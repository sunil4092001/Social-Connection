using MediatR;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Posts.Commands.CreatePost;

public class CreatePostCommandHandler(IPostRepository repository) : IRequestHandler<CreatePostCommand, PostDto>
{
    public async Task<PostDto> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var post = new Post
        {
            PostId = Guid.NewGuid(),
            AuthorUserId = request.AuthorUserId,
            Content = request.Content.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        repository.AddPost(post);
        await repository.SaveChangesAsync(cancellationToken);
        return new PostDto(post.PostId, post.AuthorUserId, post.Content, post.CreatedAt);
    }
}
