using Microsoft.EntityFrameworkCore;
using UserService.Domain;
using UserService.Infrastructure.Persistence.Configuations;

namespace UserService.Infrastructure.Persistence;

public sealed class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
    }


}
