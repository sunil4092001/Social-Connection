using MediatR;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Comments.Commands.CreateCommand
{
    public class CreateCommentCommandHandler(IPostRepository repository, IIntegrationEventPublisher eventPublisher) : IRequestHandler<CreateCommentCommand, CommentDto>
    {
        public async Task<CommentDto> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var comment = new Comment
                {
                    CommentId = Guid.NewGuid(),
                    PostId = request.PostId,
                    UserId = request.AuthorUserId,
                    Content = request.Content.Trim(),
                    //UpdatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow

                };

                 await repository.AddPostCommentAsync(comment, cancellationToken);
                //await eventPublisher.PublishAsync("comment.created", new { post.PostId, post.AuthorUserId }, cancellationToken);

                return comment.ToCommentDto();
            }
            catch (Exception ex) 
            {
                    throw;
            }
        }
    }
}
