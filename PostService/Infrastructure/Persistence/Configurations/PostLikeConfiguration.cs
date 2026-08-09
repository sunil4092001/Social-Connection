using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostService.Domain.Entity;

namespace PostService.Infrastructure.Persistence.Configurations
{
    public class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
    {
        public void Configure(EntityTypeBuilder<PostLike> builder)
        {
            builder.ToTable("Post_Like");

            builder.HasKey(x => x.PostLikeId);

            builder.Property(x => x.CreatedAt)
                  .IsRequired();

            builder.HasOne(x => x.Posts)
                .WithMany(p => p.PostLikes)
                .HasForeignKey(x => x.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Prevent duplicate likes by the same user
            builder.HasIndex(x => new
            {
                x.PostId,
                x.UserId
            }).IsUnique();
        }
    }
}
