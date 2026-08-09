using Microsoft.EntityFrameworkCore;
using UserService.Application.Dtos.ServiceCall;
using UserService.Domain;
using UserService.Infrastructure.Persistence;
using UserService.Repositories;

namespace UserService.Infrastructure.Repositories;

public sealed class EfUserProfileRepository(UserDbContext dbContext) : IUserProfileRepository
{
    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken)
    {


        dbContext.UserProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken)
    {


        dbContext.UserProfiles.Update(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UserProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<IReadOnlyList<UserProfile>> SearchAsync(string? term, CancellationToken cancellationToken)
    {
        var query = dbContext.UserProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(profile =>
                profile.FirstName.Contains(term) ||
                profile.LastName.Contains(term) ||
                profile.DisplayName.Contains(term) ||
                profile.Headline.Contains(term));
        }

        return await query.OrderBy(profile => profile.FirstName).Take(50).ToListAsync(cancellationToken);
    }

    public async Task<List<UserSummaryDto>> GetUsersProfileAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        return await dbContext.UserProfiles
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new UserSummaryDto(
                u.Id,
                u.FirstName,
                u.LastName
                )).ToListAsync(cancellationToken);
    }

}
