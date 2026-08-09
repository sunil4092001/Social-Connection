using IdentityService.DTOs;
using IdentityService.DTOs.ServiceCall;
using IdentityService.Entities;
using IdentityService.Services.Interface;
using Microsoft.AspNetCore.Identity.Data;
using RegisterRequest = IdentityService.DTOs.RegisterRequest;

namespace IdentityService.Controllers.UserServiceCall
{
    public class UserServiceClient : IUserServiceClient
    {
        private readonly HttpClient _httpClient;

        public UserServiceClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // call user service create user api
        public async Task CreateUserAsync(Guid userId ,RegisterRequest request)
        {

            var profileRequest = new CreateUserProfileRequest
            {
                UserId = userId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DisplayName = $"{request.FirstName} {request.LastName}".Trim(),
                //Headline = "Headlines",      // or "New User"
                //About = null,
                //Location = null
            };

            var response = await _httpClient.PostAsJsonAsync(
             "api/users",
             profileRequest);

            response.EnsureSuccessStatusCode();
        }
    }
}
