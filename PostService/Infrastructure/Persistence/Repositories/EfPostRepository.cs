using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace PostService.Infrastructure.Persistence.Repositories;

public sealed class EfPostRepository(PostDbContext dbContext) : IPostRepository
{
    // ----- Posts -----

    public void AddPost(Post post) => dbContext.Posts.Add(post);

    public Task<Post?> GetPostByIdAsync(Guid postId, CancellationToken cancellationToken) =>
        dbContext.Posts.FirstOrDefaultAsync(post => post.PostId == postId && !post.IsDeleted, cancellationToken);

    public Task<PostView?> GetPostViewAsync(Guid postId, Guid viewerId, CancellationToken cancellationToken) =>
        Project(dbContext.Posts.Where(post => post.PostId == postId && !post.IsDeleted), viewerId).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PostView>> GetPostViewsAsync(IReadOnlyCollection<Guid>? authorIds, Guid viewerId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.Posts.Where(post => !post.IsDeleted);
        if (authorIds is not null) query = query.Where(post => authorIds.Contains(post.AuthorUserId));

        return await Project(query.OrderByDescending(post => post.CreatedAt).Skip(skip).Take(take), viewerId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Counts and "liked by me" are computed by PostgreSQL in the same query (no N+1).</summary>
    private static IQueryable<PostView> Project(IQueryable<Post> posts, Guid viewerId) =>
        posts.AsNoTracking().Select(post => new PostView(
            post.PostId,
            post.AuthorUserId,
            post.Content,
            post.ImageUrl,
            post.CreatedAt,
            post.UpdatedAt,
            post.PostLikes!.Count(),
            post.Comments!.Count(),
            post.PostLikes!.Any(like => like.UserId == viewerId)));

    // ----- Likes -----

    public Task<PostLike?> GetLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.PostLikes.FirstOrDefaultAsync(like => like.PostId == postId && like.UserId == userId, cancellationToken);

    public void AddLike(PostLike like) => dbContext.PostLikes.Add(like);

    public void RemoveLike(PostLike like) => dbContext.PostLikes.Remove(like);

    public Task<int> CountLikesAsync(Guid postId, CancellationToken cancellationToken) =>
        dbContext.PostLikes.CountAsync(like => like.PostId == postId, cancellationToken);

    // ----- Comments -----

    public void AddComment(Comment comment) => dbContext.Comments.Add(comment);

    public Task<Comment?> GetCommentByIdAsync(Guid commentId, CancellationToken cancellationToken) =>
        dbContext.Comments
            .Include(comment => comment.Posts)
            .FirstOrDefaultAsync(comment => comment.CommentId == commentId && !comment.Posts.IsDeleted, cancellationToken);

    public void RemoveComment(Comment comment) => dbContext.Comments.Remove(comment);

    public async Task<IReadOnlyList<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken) =>
        await dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.PostId == postId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);

    // ----- Unit of work -----

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Only the (PostId, UserId) like index is unique: a concurrent duplicate like.
            throw new ConflictException("You have already liked this post.");
        }
    }
}
