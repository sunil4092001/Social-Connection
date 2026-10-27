using IdentityService.DTOs;
using IdentityService.DTOs.User;

namespace IdentityService.Services;

public interface IAuthService
{
    /// <summary>Null when the email is already registered.</summary>
    Task<RegisterResponse?> Register(RegisterRequest request);

    /// <summary>Undo a registration whose profile could not be created in UserService.</summary>
    Task RemoveUser(Guid userId);

    /// <summary>Null when the credentials are wrong.</summary>
    Task<LoginResponse?> Login(LoginRequest request);

    /// <summary>Rotates the refresh token. Null when it is unknown, expired or already used.</summary>
    Task<LoginResponse?> RefreshToken(string refreshToken);

    /// <summary>Revokes the refresh token. Unknown tokens are ignored.</summary>
    Task Logout(string refreshToken);
}
