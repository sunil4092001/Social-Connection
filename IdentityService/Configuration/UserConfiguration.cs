using IdentityService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Configuration
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(100);

            //builder.Property(x => x.PasswordHash)
            //    .IsRequired();

            //builder.Property(x => x.FirstName)
            //    .IsRequired()
            //    .HasMaxLength(50);

            //builder.Property(x => x.LastName)
            //    .IsRequired()
            //    .HasMaxLength(50);

            //builder.Property(x => x.PhoneNumber)
            //    .IsRequired(false);

            builder.HasIndex(x => x.Email)
                .IsUnique();
        }
    }
}
