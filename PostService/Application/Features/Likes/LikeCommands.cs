using BuildingBlocks.Events;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Messaging.Outbox;
using FluentValidation;
using MediatR;
using PostService.Application.Features.Posts;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Likes;

// Both commands are idempotent: liking twice or unliking a post you never liked simply returns the current state.

public sealed record LikePostCommand(Guid PostId, Guid UserId) : IRequest<PostLikeStateDto>;
public sealed record UnlikePostCommand(Guid PostId, Guid UserId) : IRequest<PostLikeStateDto>;

public sealed class LikePostValidator : AbstractValidator<LikePostCommand>
{
    public LikePostValidator() { RuleFor(c => c.PostId).NotEmpty(); RuleFor(c => c.UserId).NotEmpty(); }
}

public sealed class UnlikePostValidator : AbstractValidator<UnlikePostCommand>
{
    public UnlikePostValidator() { RuleFor(c => c.PostId).NotEmpty(); RuleFor(c => c.UserId).NotEmpty(); }
}

public sealed class LikePostCommandHandler(IPostRepository repository, IUserServiceClient users, IEventOutbox outbox)
    : IRequestHandler<LikePostCommand, PostLikeStateDto>
{
    public async Task<PostLikeStateDto> Handle(LikePostCommand request, CancellationToken cancellationToken)
    {
        var post = await repository.GetPostByIdAsync(request.PostId, cancellationToken)
            ?? throw new KeyNotFoundException("Post was not found.");

        if (await repository.GetLikeAsync(request.PostId, request.UserId, cancellationToken) is null)
        {
            var now = DateTime.UtcNow;
            repository.AddLike(new PostLike { PostLikeId = Guid.NewGuid(), PostId = post.PostId, UserId = request.UserId, CreatedAt = now });

            // Tell the author, but not about liking your own post.
            if (post.AuthorUserId != request.UserId)
            {
                var likerName = await PostPresentation.NameOfAsync(users, request.UserId, cancellationToken);
                outbox.Add(EventTopics.PostLiked, post.PostId.ToString(), new PostLikedEvent(
                    PostLikedEvent.StableEventId(post.PostId, request.UserId), post.PostId, post.AuthorUserId,
                    request.UserId, likerName, PostPresentation.Excerpt(post.Content), now));
            }

            try
            {
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (ConflictException)
            {
                // A double click raced us and the like already exists: the outcome is the same.
            }
        }

        return new PostLikeStateDto(post.PostId, await repository.CountLikesAsync(post.PostId, cancellationToken), LikedByMe: true);
    }
}

public sealed class UnlikePostCommandHandler(IPostRepository repository) : IRequestHandler<UnlikePostCommand, PostLikeStateDto>
{
    public async Task<PostLikeStateDto> Handle(UnlikePostCommand request, CancellationToken cancellationToken)
    {
        _ = await repository.GetPostByIdAsync(request.PostId, cancellationToken)
            ?? throw new KeyNotFoundException("Post was not found.");

        var like = await repository.GetLikeAsync(request.PostId, request.UserId, cancellationToken);
        if (like is not null)
        {
            repository.RemoveLike(like);
            await repository.SaveChangesAsync(cancellationToken);
        }

        return new PostLikeStateDto(request.PostId, await repository.CountLikesAsync(request.PostId, cancellationToken), LikedByMe: false);
    }
}
