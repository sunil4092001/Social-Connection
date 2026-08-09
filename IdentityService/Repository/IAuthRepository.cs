using IdentityService.DTOs.User;
using IdentityService.Entities;

namespace IdentityService.Repository
{
    public interface IAuthRepository
    {

        Task RegisterAsync(User request);
        Task<User?> GetByEmailAsync(string email);
        Task<RefreshToken> GetRefreshTokenAsync(string token);

        Task AddRefreshTokenAsync(RefreshToken token);

        Task SaveChangesAsync();

        //Task<List<UserSummaryDto>> GetUsersProfileAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);

        #region ==Profile
        Task<User> UpdateProfileAsync(User user);
        Task<User?> GetProfile(Guid userID);
        #endregion
    }
}
