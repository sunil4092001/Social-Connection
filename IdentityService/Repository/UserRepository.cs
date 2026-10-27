using IdentityService.Data;
using IdentityService.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Repository;

public class UserRepository(IdentityDbContext context) : IAuthRepository
{
    public async Task RegisterAsync(User request)
    {
        await context.AddAsync(request);
        await context.SaveChangesAsync();
    }

    public Task DeleteUserAsync(Guid userId) =>
        context.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();

    public Task<User?> GetByEmailAsync(string email)
    {
        var normalized = email.Trim().ToLower();
        return context.Users.FirstOrDefaultAsync(user => user.Email.ToLower() == normalized);
    }

    public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash) =>
        context.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.Token == tokenHash);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensAsync(Guid userId, DateTime now) =>
        await context.RefreshTokens
            .Where(token => token.UserId == userId && !token.IsRevoked && token.ExpiryDate > now)
            .ToListAsync();

    public async Task AddRefreshTokenAsync(RefreshToken token)
    {
        await context.RefreshTokens.AddAsync(token);
    }

    public Task SaveChangesAsync() => context.SaveChangesAsync();
}
