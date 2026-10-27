using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostService.Domain.Entity;

namespace PostService.Infrastructure.Persistence.Configurations
{
    public class PostConfiguration : IEntityTypeConfiguration<Post>
    {
        public void Configure(EntityTypeBuilder<Post> builder)
        {
            builder.ToTable("Post");

            builder.HasKey(x => x.PostId);

            builder.Property(x => x.Content)
                .IsRequired()
                .HasMaxLength(10000);

            builder.Property(x => x.ImageUrl)
              .HasMaxLength(500);

            builder.Property(x => x.CreatedAt)
                   .IsRequired();

            builder.Property(x => x.Visibility)
                .IsRequired();

            // Feed (newest first, deleted posts excluded) and profile (one author's posts).
            builder.HasIndex(x => new { x.IsDeleted, x.CreatedAt });
            builder.HasIndex(x => new { x.AuthorUserId, x.CreatedAt });

            /* we can configuration relation from anyone place
             * 
             * // One Post -> Many Comments
        builder.HasMany(x => x.Comments)
               .WithOne(x => x.Post)
               .HasForeignKey(x => x.PostId)
               .OnDelete(DeleteBehavior.Cascade);

        // One Post -> Many Likes
        builder.HasMany(x => x.Likes)
               .WithOne(x => x.Post)
               .HasForeignKey(x => x.PostId)
               .OnDelete(DeleteBehavior.Cascade);
             * 
             */


        }
    }
}
