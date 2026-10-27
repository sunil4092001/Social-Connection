using BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using PostService.Domain.Entity;
using PostService.Infrastructure.Persistence.Configurations;

namespace PostService.Infrastructure.Persistence;

public sealed class PostDbContext(DbContextOptions<PostDbContext> options) : DbContext(options)
{
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<CommentLike> CommentLiks => Set<CommentLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new PostConfiguration());
        modelBuilder.ApplyConfiguration(new CommentConfiguration());
        modelBuilder.ApplyConfiguration(new PostLikeConfiguration());
        modelBuilder.ApplyConfiguration(new CommentLikeConfiguration());
        // Like and comment events are stored here in the same transaction and relayed to Kafka.
        modelBuilder.ConfigureOutbox();
        //need to map post configuration dirctly
        //modelBuilder.Entity<Post>(entity =>
        //{
        //    entity.HasKey(post => post.PostId);
        //    entity.Property(post => post.AuthorUserId).IsRequired();
        //    entity.Property(post => post.Content).HasMaxLength(3_000).IsRequired();
        //    entity.Property(post => post.CreatedAt).IsRequired();
        //    entity.HasIndex(post => post.CreatedAt);
        //});

        //modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        //This automatically finds every class that implements IEntityTypeConfiguration<T> in the same assembly.
    }
}

