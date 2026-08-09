using IdentityService.DTOs;

namespace IdentityService.Services.Interface
{
    public interface IUserServiceClient
    {
        Task CreateUserAsync(Guid userId, RegisterRequest request);
    }
}
