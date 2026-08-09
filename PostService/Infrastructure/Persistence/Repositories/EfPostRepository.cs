using Microsoft.EntityFrameworkCore;
using PostService.Application;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;
using PostService.Infrastructure.Persistence;

namespace PostService.Infrastructure.Persistence.Repositories;

public sealed class EfPostRepository(PostDbContext dbContext) : IPostRepository
{

    #region ================[Post Operation]==================
    public async Task AddAsync(Post post, CancellationToken cancellationToken)
    {
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
 
    public async Task<IReadOnlyList<Post>> GetFeedAsync(int skip, int take, CancellationToken cancellationToken) =>
        await dbContext.Posts.AsNoTracking()
            .OrderByDescending(post => post.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<Post?> GetPostByIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Posts.AsNoTracking()
                      .Include(p => p.Comments)
                      .FirstOrDefaultAsync(post => post.PostId == postId, cancellationToken);
        //return await dbContext.Posts.AsNoTracking()
        //              .FirstOrDefaultAsync(post => post.PostId == postId, cancellationToken);
        
    }

    //public async Task<Post?> GetPostByIdAsync(Guid postId, CancellationToken cancellationToken = default)
    //{
    //    return await dbContext.Posts.AsNoTracking()
    //                  .Include(p => p.Comments)
    //                        .ThenInclude(c => c.CommentLikes)
    //                  .Include(p => p.PostLikes)
    //                  .FirstOrDefaultAsync(post => post.PostId == postId, cancellationToken);
    //    //return await dbContext.Posts.AsNoTracking()
    //    //              .FirstOrDefaultAsync(post => post.PostId == postId, cancellationToken);
        
    //}

    public async Task UpdateAsync(Post post, CancellationToken cancellationToken = default)
    {
        dbContext.Update(post);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Post post, CancellationToken cancellationToken = default)
    {
        dbContext.Remove(post);
       /*
        Soft deleted
        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;

        dbContext.Posts.Update(post);
       */
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    #endregion


    #region ================[Comment Operation]==================

    public async Task AddPostCommentAsync(Comment comment, CancellationToken cancellationToken)
    {
        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken)
    {
        return await dbContext.Comments
                    .AsNoTracking()
                    .Where(c => c.PostId == postId)
                    .Include(c => c.CommentLikes)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync(cancellationToken);
    }

    #endregion

    #region ================[Comment like Operation]==================

    #endregion

    #region ================[Post Like Operation]==================

    #endregion

    #region ================[]==================

    #endregion
}
