using System.Security.Claims;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Response;
using FluentValidation;
using IdentityService.DTOs;
using IdentityService.DTOs.User;
using IdentityService.Services;
using IdentityService.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IAuthService authService,
    IUserServiceClient userServiceClient,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Creates the login (here) and the profile (UserService). If UserService fails, the login is removed again so
    /// the user can simply retry; no half-registered accounts are left behind.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await authService.Register(request)
            ?? throw new ConflictException("Email already exists.");

        try
        {
            await userServiceClient.CreateUserAsync(result.UserId, request);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Profile creation failed for new user {UserId}; rolling back the registration", result.UserId);
            await authService.RemoveUser(result.UserId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponseFactory.Failure<RegisterResponse>(
                "Registration could not be completed right now. Please try again in a moment."));
        }

        return Ok(ApiResponseFactory.Success(result, "User registered successfully."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await authService.Login(request);
        if (result is null)
            return Unauthorized(ApiResponseFactory.Failure<LoginResponse>("Invalid email or password."));

        return Ok(ApiResponseFactory.Success(result, "Login successful."));
    }

    /// <summary>Exchanges a refresh token for a new access token and a new refresh token (the old one stops working).</summary>
    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> RefreshToken(RefreshTokenRequest request)
    {
        var result = await authService.RefreshToken(request.RefreshToken);
        if (result is null)
            return Unauthorized(ApiResponseFactory.Failure<LoginResponse>("Your session has expired. Please log in again."));

        return Ok(ApiResponseFactory.Success(result, "Token refreshed."));
    }

    /// <summary>Revokes the refresh token. Always succeeds so it can be called even when the session already ended.</summary>
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<bool>>> Logout(RefreshTokenRequest request)
    {
        await authService.Logout(request.RefreshToken);
        return Ok(ApiResponseFactory.Success(true, "Logged out successfully."));
    }

    [Authorize]
    [HttpGet("profile")]
    public IActionResult Profile() => Ok(new
    {
        UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        Email = User.FindFirst(ClaimTypes.Email)?.Value
    });
}
