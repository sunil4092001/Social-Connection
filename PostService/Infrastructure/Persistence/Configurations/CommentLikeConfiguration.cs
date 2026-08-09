using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostService.Domain.Entity;

namespace PostService.Infrastructure.Persistence.Configurations
{
    public class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLike>
    {
        public void Configure(EntityTypeBuilder<CommentLike> builder)
        {
            builder.ToTable("Comment_Like");

            builder.HasKey(x => x.CommentLikeId);

            builder.Property(x => x.CreatedAt)
                  .IsRequired();
            // Foreign Key -> Comment
            builder.HasOne(x => x.Comments)
                   .WithMany(x => x.CommentLikes)
                   .HasForeignKey(x => x.CommentId)
                   .OnDelete(DeleteBehavior.Cascade);


            // Prevent duplicate likes by the same user
            builder.HasIndex(x => new
            {
                x.CommentId,
                x.UserId
            }).IsUnique();
            
        }
    }
}
