using BuildingBlocks.Exceptions;
using MediatR;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts.Commands.DeletePost;

/// <summary>Soft delete: the post (with its likes and comments) disappears everywhere but stays in the database.</summary>
public sealed class DeletePostCommandHandler(IPostRepository repository) : IRequestHandler<DeletePostCommand, bool>
{
    public async Task<bool> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        var post = await repository.GetPostByIdAsync(request.PostId, cancellationToken);
        if (post is null) return false;
        if (post.AuthorUserId != request.RequestedByUserId)
            throw new ForbiddenException("Only the author can delete this post.");

        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
