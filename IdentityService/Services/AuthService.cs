using System.Security.Cryptography;
using System.Text;
using IdentityService.DTOs;
using IdentityService.DTOs.User;
using IdentityService.Entities;
using IdentityService.Repository;

namespace IdentityService.Services;

/// <summary>
/// Credentials and sessions. Access tokens are short-lived JWTs; refresh tokens are random, single use and stored
/// only as hashes. Presenting an already used refresh token is treated as theft: every session of that user ends.
/// </summary>
public class AuthService(IAuthRepository repository, JwtService jwtService, IConfiguration configuration, ILogger<AuthService> logger) : IAuthService
{
    private TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(configuration.GetValue("Jwt:RefreshTokenDays", 7));

    public async Task<RegisterResponse?> Register(RegisterRequest request)
    {
        if (await repository.GetByEmailAsync(request.Email) is not null) return null;

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await repository.RegisterAsync(user);

        return new RegisterResponse { UserId = user.Id, Email = user.Email };
    }

    public Task RemoveUser(Guid userId) => repository.DeleteUserAsync(userId);

    public async Task<LoginResponse?> Login(LoginRequest request)
    {
        var user = await repository.GetByEmailAsync(request.Email);
        // Same answer for "no such user" and "wrong password" so emails cannot be probed.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;

        return await IssueTokensAsync(user);
    }

    public async Task<LoginResponse?> RefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        var now = DateTime.UtcNow;
        var stored = await repository.GetRefreshTokenAsync(Hash(refreshToken));
        if (stored is null) return null;

        if (stored.IsRevoked)
        {
            // A token can be used once. Seeing it again means someone else has a copy: end every session of the user.
            logger.LogWarning("Reuse of revoked refresh token {TokenId} for user {UserId}; revoking all sessions", stored.Id, stored.UserId);
            foreach (var active in await repository.GetActiveRefreshTokensAsync(stored.UserId, now))
            {
                active.IsRevoked = true;
                active.RevokedAt = now;
            }
            await repository.SaveChangesAsync();
            return null;
        }

        if (stored.ExpiryDate <= now) return null;

        var (response, newHash) = await CreateTokensAsync(stored.User, now);
        stored.IsRevoked = true;
        stored.RevokedAt = now;
        stored.ReplacedByToken = newHash;
        await repository.SaveChangesAsync();
        return response;
    }

    public async Task Logout(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var stored = await repository.GetRefreshTokenAsync(Hash(refreshToken));
        if (stored is null || stored.IsRevoked) return;

        stored.IsRevoked = true;
        stored.RevokedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync();
    }

    private async Task<LoginResponse> IssueTokensAsync(User user)
    {
        var (response, _) = await CreateTokensAsync(user, DateTime.UtcNow);
        await repository.SaveChangesAsync();
        return response;
    }

    /// <summary>Creates an access token and a new refresh token (added to the context, not yet saved).</summary>
    private async Task<(LoginResponse Response, string RefreshTokenHash)> CreateTokensAsync(User user, DateTime now)
    {
        var refreshToken = jwtService.GenerateRefreshToken();
        var hash = Hash(refreshToken);
        await repository.AddRefreshTokenAsync(new RefreshToken
        {
            Token = hash,
            UserId = user.Id,
            CreatedAt = now,
            ExpiryDate = now.Add(RefreshTokenLifetime)
        });

        return (new LoginResponse { AccessToken = jwtService.GenerateToken(user), RefreshToken = refreshToken }, hash);
    }

    internal static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
