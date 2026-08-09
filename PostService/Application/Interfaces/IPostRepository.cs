using PostService.Domain.Entity;

namespace PostService.Application.Interfaces
{
    public interface IPostRepository
    {

        #region ================[Post Operation]==================
        Task AddAsync(Post post, CancellationToken cancellationToken);
        Task<Post?> GetPostByIdAsync(Guid postId, CancellationToken cancellationToken);
        Task<IReadOnlyList<Post>> GetFeedAsync(int skip, int take, CancellationToken cancellationToken);
        Task UpdateAsync(Post post, CancellationToken cancellationToken = default);
        Task DeleteAsync(Post post, CancellationToken cancellationToken = default);

        #endregion

        #region ================[Comment Operation]==================

        Task AddPostCommentAsync(Comment comment, CancellationToken cancellationToken);

        Task<IReadOnlyList<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken);

        #endregion

        #region ================[comment Like Operation]==================

        #endregion

        #region ================[Post like Operation]==================

        #endregion

    }
}
