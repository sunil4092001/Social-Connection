using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Domain;

namespace UserService.Infrastructure.Persistence.Configuations;

public sealed class ProfessionalSkillConfiguration : IEntityTypeConfiguration<ProfessionalSkill>
{
    public void Configure(EntityTypeBuilder<ProfessionalSkill> builder)
    {
        builder.ToTable("ProfessionalSkills");
        builder.HasKey(x => new { x.ProfessionalId, x.SkillId });
        builder.Property(x => x.SkillLevel).HasConversion<string>().IsRequired();
        builder.Property(x => x.ExperienceYears).IsRequired();
        builder.HasOne(x => x.Professional)
            .WithMany(x => x.ProfessionalSkills)
            .HasForeignKey(x => x.ProfessionalId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Skill)
            .WithMany(x => x.ProfessionalSkills)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
