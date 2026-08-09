using MediatR;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Features.Posts.Mapping;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Posts.Commands.CreatePost
{
    public class CreatePostCommandHandler(IPostRepository repository,
    IIntegrationEventPublisher eventPublisher) : IRequestHandler<CreatePostCommand, PostDto>
    {
        public async Task<PostDto> Handle(CreatePostCommand request, CancellationToken cancellationToken)
        {
            var post = new Post
            {
                PostId = Guid.NewGuid(),
                AuthorUserId = request.AuthorUserId,
                Content = request.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(post, cancellationToken);
            await eventPublisher.PublishAsync("posts.created", new { post.PostId, post.AuthorUserId }, cancellationToken);

            return post.ToDto();
        }
    }
}
