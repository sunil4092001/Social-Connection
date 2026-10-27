using BuildingBlocks.Exceptions;
using MediatR;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts.Commands.UpdatePost;

public sealed class UpdatePostCommandHandler(IPostRepository repository) : IRequestHandler<UpdatePostCommand, PostDto?>
{
    public async Task<PostDto?> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
    {
        var post = await repository.GetPostByIdAsync(request.PostId, cancellationToken);
        if (post is null) return null;
        if (post.AuthorUserId != request.EditorUserId)
            throw new ForbiddenException("Only the author can edit this post.");

        post.Content = request.Content.Trim();
        post.ImageUrl = request.ImageUrl ?? post.ImageUrl;
        post.Visibility = request.Visibility ?? post.Visibility;
        post.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return new PostDto(post.PostId, post.AuthorUserId, post.Content, post.CreatedAt);
    }
}
