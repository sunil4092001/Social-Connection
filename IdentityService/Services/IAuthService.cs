using IdentityService.DTOs;
using IdentityService.DTOs.User;

namespace IdentityService.Services
{
    public interface IAuthService
    {
        Task<RegisterResponse?> Register(RegisterRequest request);
        Task<LoginResponse> Login(LoginRequest request);

        Task<LoginResponse> RefreshToken(string refreshToken);

        Task<bool> Logout(string refreshToken);
    }
}
