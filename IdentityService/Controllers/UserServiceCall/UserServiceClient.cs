using IdentityService.DTOs.ServiceCall;
using IdentityService.Services.Interface;
using RegisterRequest = IdentityService.DTOs.RegisterRequest;

namespace IdentityService.Controllers.UserServiceCall;

/// <summary>
/// Hands a new registration to UserService. The endpoint is outside /api, so the gateway never exposes it, and it is
/// protected by the X-Internal-Service-Key header (set once in Program.cs from InternalService:UserServiceKey).
/// </summary>
public class UserServiceClient(HttpClient httpClient) : IUserServiceClient
{
    public async Task CreateUserAsync(Guid userId, RegisterRequest request)
    {
        var profileRequest = new CreateUserProfileRequest
        {
            UserId = userId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DisplayName = $"{request.FirstName} {request.LastName}".Trim(),
            Location = request.Location,
            UserType = request.UserType,
            ProfessionalType = request.ProfessionalType,
            ExperienceYears = request.ExperienceYears
        };

        var response = await httpClient.PostAsJsonAsync("internal/users/profiles", profileRequest);
        response.EnsureSuccessStatusCode();
    }
}
