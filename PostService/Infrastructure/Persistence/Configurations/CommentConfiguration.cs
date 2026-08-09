using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PostService.Domain.Entity;

namespace PostService.Infrastructure.Persistence.Configurations
{
    public class CommentConfiguration : IEntityTypeConfiguration<Comment>
    {
        public void Configure(EntityTypeBuilder<Comment> builder)
        {
            builder.ToTable("Comment");

            builder.HasKey(x => x.CommentId);

            builder.Property(x => x.Content)
                .IsRequired()
                .HasMaxLength(10000);

            builder.Property(x => x.CreatedAt)
                  .IsRequired();

            builder.HasOne(x => x.Posts) //A Comment has one Post.
                .WithMany(p => p.Comments) //A Post has many Comments.
                .HasForeignKey(c => c.PostId) //The foreign key is Comment.PostId.
                .OnDelete(DeleteBehavior.Cascade); //Deleting a post also deletes its related comments (Cascade).
            
            
            //  Created relation n child
            //// One Comment -> Many Likes 
            //builder.HasMany(x => x.Likes)
            //       .WithOne(x => x.Comment)
            //       .HasForeignKey(x => x.CommentId)
            //       .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
