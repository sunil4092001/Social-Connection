using IdentityService.Entities;

namespace IdentityService.Repository;

public interface IAuthRepository
{
    Task RegisterAsync(User request);
    /// <summary>Removes a user that could not be completed (compensation when UserService rejects the profile).</summary>
    Task DeleteUserAsync(Guid userId);
    /// <summary>Case-insensitive lookup.</summary>
    Task<User?> GetByEmailAsync(string email);
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash);
    Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensAsync(Guid userId, DateTime now);
    Task AddRefreshTokenAsync(RefreshToken token);
    Task SaveChangesAsync();
}
