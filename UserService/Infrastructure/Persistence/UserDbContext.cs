using Microsoft.EntityFrameworkCore;
using UserService.Domain;
using UserService.Infrastructure.Persistence.Configuations;

namespace UserService.Infrastructure.Persistence;

public sealed class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ProfessionalSkill> ProfessionalSkills => Set<ProfessionalSkill>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
        modelBuilder.ApplyConfiguration(new SkillConfiguration());
        modelBuilder.ApplyConfiguration(new ProfessionalSkillConfiguration());
    }


}
