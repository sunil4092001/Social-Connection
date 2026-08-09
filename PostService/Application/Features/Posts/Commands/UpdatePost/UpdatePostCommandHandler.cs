using MediatR;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts.Commands.UpdatePost
{
    public sealed class UpdatePostCommandHandler : IRequestHandler<UpdatePostCommand, PostDto?>
    {
        private readonly IPostRepository _repository;

        public UpdatePostCommandHandler(IPostRepository repository)
        {
            this._repository = repository;  
        }
        public async Task<PostDto?> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
        {
            var post = await _repository.GetPostByIdAsync(request.PostId, cancellationToken);

            if (post == null) 
                return null;

            post.Content = request.Content;
            post.ImageUrl = request.ImageUrl;
            post.Visibility = request.Visibility;
            post.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(post);

            return new PostDto(
               post.PostId,
               post.AuthorUserId,
               post.Content,
               post.CreatedAt);

        }
    }
}
