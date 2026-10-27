using BuildingBlocks.Events;
using BuildingBlocks.Messaging.Outbox;
using MediatR;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Posts;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Comments.Commands.CreateCommand;

public class CreateCommentCommandHandler(IPostRepository repository, IUserServiceClient users, IEventOutbox outbox)
    : IRequestHandler<CreateCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
    {
        // Checked here so a wrong (or deleted) post returns 404 instead of a foreign-key error (500).
        var post = await repository.GetPostByIdAsync(request.PostId, cancellationToken)
            ?? throw new KeyNotFoundException("Post was not found.");

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            CommentId = Guid.NewGuid(),
            PostId = request.PostId,
            UserId = request.AuthorUserId,
            Content = request.Content.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        repository.AddComment(comment);

        var commenterName = await PostPresentation.NameOfAsync(users, request.AuthorUserId, cancellationToken);
        if (post.AuthorUserId != request.AuthorUserId)
        {
            outbox.Add(EventTopics.PostCommented, post.PostId.ToString(), new PostCommentedEvent(
                Guid.NewGuid(), post.PostId, comment.CommentId, post.AuthorUserId, request.AuthorUserId,
                commenterName, PostPresentation.Excerpt(comment.Content), now));
        }

        await repository.SaveChangesAsync(cancellationToken);
        return comment.ToCommentDto(commenterName, request.AuthorUserId, post.AuthorUserId);
    }
}
