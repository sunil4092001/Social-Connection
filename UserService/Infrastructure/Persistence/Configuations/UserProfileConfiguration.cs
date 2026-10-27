using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Domain;

namespace UserService.Infrastructure.Persistence.Configuations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(profile => profile.Id);
        builder.HasIndex(profile => profile.UserId).IsUnique();
        builder.Property(profile => profile.FirstName).IsRequired().HasMaxLength(200);
        builder.Property(profile => profile.LastName).IsRequired().HasMaxLength(200);
        builder.Property(profile => profile.DisplayName).IsRequired().HasMaxLength(120);
        builder.Property(profile => profile.Headline).IsRequired().HasMaxLength(160);
        builder.Property(profile => profile.Location).HasMaxLength(120);
        builder.Property(profile => profile.UserType).HasConversion<string>().IsRequired().HasDefaultValue(UserType.CUSTOMER);
        builder.Property(profile => profile.ProfessionalType).HasConversion<string>();
        builder.Property(profile => profile.AvailabilityStatus).HasConversion<string>();
        builder.Property(profile => profile.IsVerified).HasDefaultValue(false);
        builder.Property(profile => profile.CreatedAtUtc).IsRequired();
        builder.Property(profile => profile.UpdatedAt).IsRequired();
    }
}
