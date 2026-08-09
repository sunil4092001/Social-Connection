using System.Security.Claims;
using BuildingBlocks.Response;
using FluentValidation;
using IdentityService.Controllers.UserServiceCall;
using IdentityService.Data;
using IdentityService.DTOs;
using IdentityService.DTOs.User;
using IdentityService.Entities;
using IdentityService.Services;
using IdentityService.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {

        private readonly AuthService _authService;
        private readonly IUserServiceClient _userServiceClient;

        public AuthController(AuthService authService, JwtService jwtService, IUserServiceClient userServiceClient)
        {
            _authService = authService;
            _userServiceClient = userServiceClient;
        }


        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request, [FromServices] IValidator<RegisterRequest> validator)
        {

            try
            {
                var validationResult = await validator.ValidateAsync(request);

                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors
                        .GroupBy(x => x.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(x => x.ErrorMessage).ToArray()
                        );

                    return BadRequest(ApiResponseFactory.Failure<RegisterResponse>(
                        "Validation Message :",
                        errors
                        ));
                }

                var result = await _authService.Register(request);
                if (result == null)
                {
                    return Conflict(
                        ApiResponseFactory.Failure<RegisterResponse>(
                            "Email already exists."));
                }

                try
                {
                    //Calling UserService Api 
                    await _userServiceClient.CreateUserAsync(result.UserId, request);

                }
                catch (Exception ex)
                {                 
                    throw ex;
                }

                return Ok(ApiResponseFactory.Success(
                    result,
                    "User registered successfully."
                    ));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _authService.Login(request);

            if (result == null)
                //return Unauthorized("Invalid Credential");
                return Unauthorized(ApiResponseFactory.Failure<LoginResponse>(
                    "Invalid email or password."
                    ));     
            return Ok(ApiResponseFactory.Success<LoginResponse>(
                    result,
                    "Login successful."
                ));
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] string token)
        {
            var result = await _authService.RefreshToken(token);
            return Ok(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] string token)
        {
            var result = await _authService.Logout(token);

            if (!result)
                return BadRequest("Invalid token");

            return Ok("Logged out successfully");
        }

        [Authorize]
        [HttpGet("profile")]
        public IActionResult Profile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var name = User.FindFirst(ClaimTypes.Name)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            //var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                UserId = userId,
                Name = name,
                Email = email,
                
                //Role = role
            });
        }
    }
}
