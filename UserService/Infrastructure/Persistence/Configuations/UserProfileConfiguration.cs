using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Domain;

namespace UserService.Infrastructure.Persistence.Configuations
{
    public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.ToTable("UserProfiles");

            builder.HasKey(profile => profile.Id);

            builder.Property(profile => profile.FirstName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(profile => profile.LastName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(profile => profile.DisplayName)
                .IsRequired()
                .HasMaxLength(120);

            builder.Property(profile => profile.Headline)
                .IsRequired()
                .HasMaxLength(160);

            builder.HasIndex(x => x.UserId)
                .IsUnique();

            builder.Property(x => x.CreatedAtUtc)
                  .IsRequired();

          
        }
    }
}

