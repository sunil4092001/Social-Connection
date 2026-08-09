using IdentityService.DTOs.User;
using IdentityService.Entities;
using IdentityService.Repository;

namespace IdentityService.Services.Interface
{
    public interface IProfileService 
    
    {
                  
        Task<UserProfileDto?> GetProfileAsync(Guid UserId);

        Task<UserProfileDto> UpdateProfileAsync(UserProfileDto profileDto);

        Task SaveChangesAsync();

        //Task<List<UserSummaryDto>> GetUsersProfileAsync(IEnumerable<Guid> UserIds, CancellationToken cancellationToken);
    }

}
