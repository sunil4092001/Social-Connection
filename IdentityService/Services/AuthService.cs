using IdentityService.Data;
using IdentityService.DTOs;
using IdentityService.DTOs.User;
using IdentityService.Entities;
using IdentityService.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserRepository _repository;
        private readonly JwtService _jwtService;

        public AuthService(UserRepository context, JwtService jwtService)
        {
            _repository = context;
            _jwtService = jwtService;
        }

        public async Task<RegisterResponse?> Register(RegisterRequest request)
        {
            var existingUser =  await _repository.GetByEmailAsync(request.Email);

            if (existingUser != null)
            {
                return null;
                //throw new Exception("Email already exists");
            }

            var user = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                //FirstName = request.FirstName,
                //LastName = request.LastName
            };

            await _repository.RegisterAsync(user);
          
            return new RegisterResponse
            {
                UserId = user.Id,
                Email = user.Email
            };
        }

        public async Task<LoginResponse> Login(LoginRequest request)
        {

            var user = await _repository.GetByEmailAsync(request.Email);

            if (user == null)
                return null ;

            bool valid = BCrypt.Net.BCrypt.Verify( request.Password, user.PasswordHash);

            if (!valid)            
                return null;
            
            var accessToken = _jwtService.GenerateToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                Token = refreshToken,
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                UserId = user.Id
            };

            await _repository.AddRefreshTokenAsync(refreshTokenEntity);
            await _repository.SaveChangesAsync();

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
          
        }

        public async Task<LoginResponse> RefreshToken(string refreshToken)
        {
            var storedToken = await _repository.GetRefreshTokenAsync(refreshToken);

            if (storedToken == null)
                throw new Exception("Invalid refresh token");

            if (storedToken.IsRevoked)
                throw new Exception("Token revoked");

            if (storedToken.ExpiryDate < DateTime.UtcNow)
                throw new Exception("Token expired");

            var newAccessToken = _jwtService.GenerateToken(storedToken.User);

            return new LoginResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = storedToken.Token
            };
        }

        public async Task<bool> Logout(string refreshToken)
        {
            var storedToken = await _repository.GetRefreshTokenAsync(refreshToken);

            if (storedToken == null)
                return false;

            storedToken.IsRevoked = true;

            await _repository.SaveChangesAsync();

            return true;
        }
    }
}
