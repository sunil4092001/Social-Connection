using Microsoft.EntityFrameworkCore;
using RequirementService.Domain;
using BuildingBlocks.Messaging.Outbox;

namespace RequirementService.Infrastructure;

public sealed class RequirementDbContext(DbContextOptions<RequirementDbContext> options) : DbContext(options)
{
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<RequirementSkill> RequirementSkills => Set<RequirementSkill>();
    public DbSet<RequirementResponse> RequirementResponses => Set<RequirementResponse>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Requirement>(e =>
        {
            e.ToTable("Requirements");
            e.HasKey(x => x.Id);
            // Ids are created by the domain, so EF must treat a new entity with an id as "Added", not "existing".
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ProfessionalType).HasMaxLength(80).IsRequired();
            e.Property(x => x.Location).HasMaxLength(120).IsRequired();
            e.Property(x => x.ExpectedDuration).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().IsRequired();
            // Maps to PostgreSQL xmin: two simultaneous decisions on one requirement cannot both succeed.
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.ProfessionalType, x.Location, x.Status });
            e.HasIndex(x => x.CustomerId);
        });

        modelBuilder.Entity<RequirementSkill>(e =>
        {
            e.ToTable("RequirementSkills");
            e.HasKey(x => new { x.RequirementId, x.SkillId });
            e.HasOne(x => x.Requirement).WithMany(x => x.RequiredSkills).HasForeignKey(x => x.RequirementId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RequirementResponse>(e =>
        {
            e.ToTable("RequirementResponses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<string>().IsRequired();
            // One response per professional per requirement, enforced by the database as well as the domain.
            e.HasIndex(x => new { x.RequirementId, x.ProfessionalId }).IsUnique();
            e.HasIndex(x => x.ProfessionalId);
            e.HasOne(x => x.Requirement).WithMany(x => x.Responses).HasForeignKey(x => x.RequirementId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.ConfigureOutbox();
    }
}
