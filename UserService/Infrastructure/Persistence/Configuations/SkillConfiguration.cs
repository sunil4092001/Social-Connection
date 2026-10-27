using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Domain;

namespace UserService.Infrastructure.Persistence.Configuations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.Name).IsRequired().HasMaxLength(120);
        builder.Property(skill => skill.Category).IsRequired().HasMaxLength(120);
        builder.Property(skill => skill.CreatedAtUtc).IsRequired();
        builder.HasIndex(skill => new { skill.Name, skill.Category }).IsUnique();
        builder.HasData(
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a001", "Brick Work", "Masonry"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a002", "Concrete Work", "Masonry"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a003", "Plastering", "Masonry"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a004", "House Construction", "Masonry"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a005", "Furniture", "Carpentry"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a006", "Modular Kitchen", "Carpentry"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a007", "Bathroom Plumbing", "Plumbing"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a008", "Kitchen Plumbing", "Plumbing"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a009", "Water Pipeline", "Plumbing"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a010", "House Wiring", "Electrical"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a011", "Electrical Installation", "Electrical"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a012", "Lighting", "Electrical"),
            Seed("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a013", "Painter", "Painter"));
    }

    private static Skill Seed(string id, string name, string category) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        Category = category,
        CreatedAtUtc = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc)
    };
}
