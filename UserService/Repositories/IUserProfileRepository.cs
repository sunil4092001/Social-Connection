using UserService.Application.Dtos.ServiceCall;
using UserService.Domain;

namespace UserService.Repositories;

public interface IUserProfileRepository
{
    Task AddAsync(UserProfile profile, CancellationToken cancellationToken);
    Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken);
    Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserProfile>> SearchAsync(string? term, CancellationToken cancellationToken);
    Task<List<UserSummaryDto>> GetUsersProfileAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}
