using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace LinkedIn.Tests.Fakes;

/// <summary>In-memory IPostRepository that mirrors the EF repository's rules (soft delete, counts, staged writes).</summary>
public sealed class InMemoryPostRepository : IPostRepository
{
    public List<Post> Posts { get; } = [];
    public List<PostLike> Likes { get; } = [];
    public List<Comment> Comments { get; } = [];
    public int SaveCount { get; private set; }

    private readonly List<Action> staged = [];

    public Post Seed(Guid authorId, string content = "Post content", DateTime? createdAt = null)
    {
        var post = new Post { PostId = Guid.NewGuid(), AuthorUserId = authorId, Content = content, CreatedAt = createdAt ?? DateTime.UtcNow };
        post.UpdatedAt = post.CreatedAt;
        Posts.Add(post);
        return post;
    }

    public void AddPost(Post post) => staged.Add(() => Posts.Add(post));

    public Task<Post?> GetPostByIdAsync(Guid postId, CancellationToken cancellationToken) =>
        Task.FromResult(Posts.FirstOrDefault(p => p.PostId == postId && !p.IsDeleted));

    public Task<PostView?> GetPostViewAsync(Guid postId, Guid viewerId, CancellationToken cancellationToken) =>
        Task.FromResult(Posts.Where(p => p.PostId == postId && !p.IsDeleted).Select(p => View(p, viewerId)).FirstOrDefault());

    public Task<IReadOnlyList<PostView>> GetPostViewsAsync(IReadOnlyCollection<Guid>? authorIds, Guid viewerId, int skip, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PostView>>(Posts
            .Where(p => !p.IsDeleted && (authorIds is null || authorIds.Contains(p.AuthorUserId)))
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip).Take(take)
            .Select(p => View(p, viewerId))
            .ToList());

    private PostView View(Post p, Guid viewerId) => new(p.PostId, p.AuthorUserId, p.Content, p.ImageUrl, p.CreatedAt, p.UpdatedAt,
        Likes.Count(l => l.PostId == p.PostId), Comments.Count(c => c.PostId == p.PostId), Likes.Any(l => l.PostId == p.PostId && l.UserId == viewerId));

    public Task<PostLike?> GetLikeAsync(Guid postId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Likes.FirstOrDefault(l => l.PostId == postId && l.UserId == userId));
    public void AddLike(PostLike like) => staged.Add(() => Likes.Add(like));
    public void RemoveLike(PostLike like) => staged.Add(() => Likes.Remove(like));
    public Task<int> CountLikesAsync(Guid postId, CancellationToken cancellationToken) => Task.FromResult(Likes.Count(l => l.PostId == postId));

    public void AddComment(Comment comment) => staged.Add(() => Comments.Add(comment));
    public Task<Comment?> GetCommentByIdAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var comment = Comments.FirstOrDefault(c => c.CommentId == commentId);
        if (comment is null) return Task.FromResult<Comment?>(null);
        comment.Posts = Posts.Single(p => p.PostId == comment.PostId);
        return Task.FromResult(comment.Posts.IsDeleted ? null : comment);
    }
    public void RemoveComment(Comment comment) => staged.Add(() => Comments.Remove(comment));
    public Task<IReadOnlyList<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Comment>>(Comments.Where(c => c.PostId == postId).OrderBy(c => c.CreatedAt).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        staged.ForEach(apply => apply());
        staged.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }
}
