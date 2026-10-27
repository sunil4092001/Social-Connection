using PostService.Domain.Entity;

namespace PostService.Application.Interfaces;

/// <summary>A post as one viewer sees it: counts are computed in the database query.</summary>
public sealed record PostView(
    Guid PostId,
    Guid AuthorUserId,
    string Content,
    string? ImageUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int LikeCount,
    int CommentCount,
    bool LikedByViewer);

/// <summary>
/// Posts, comments and likes. Write methods only stage changes; handlers call SaveChangesAsync once so the change
/// and its outbox event are committed together. Soft-deleted posts are never returned.
/// </summary>
public interface IPostRepository
{
    // Posts
    void AddPost(Post post);
    /// <summary>Tracked entity for updates; null when missing or deleted.</summary>
    Task<Post?> GetPostByIdAsync(Guid postId, CancellationToken cancellationToken);
    Task<PostView?> GetPostViewAsync(Guid postId, Guid viewerId, CancellationToken cancellationToken);
    /// <summary>Newest first. <paramref name="authorIds"/> null means every author.</summary>
    Task<IReadOnlyList<PostView>> GetPostViewsAsync(IReadOnlyCollection<Guid>? authorIds, Guid viewerId, int skip, int take, CancellationToken cancellationToken);

    // Likes
    Task<PostLike?> GetLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken);
    void AddLike(PostLike like);
    void RemoveLike(PostLike like);
    Task<int> CountLikesAsync(Guid postId, CancellationToken cancellationToken);

    // Comments
    void AddComment(Comment comment);
    /// <summary>Tracked comment including its post (for the post-author check); null when missing or the post is deleted.</summary>
    Task<Comment?> GetCommentByIdAsync(Guid commentId, CancellationToken cancellationToken);
    void RemoveComment(Comment comment);
    Task<IReadOnlyList<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>Commits staged changes (and outbox events). A duplicate like becomes a ConflictException.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
