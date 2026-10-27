using FluentValidation;
using MediatR;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Posts;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Comments.Queries;

public sealed record GetPostCommentQuery(Guid PostId, Guid ViewerId) : IRequest<IReadOnlyList<CommentDto>>;

public sealed class GetPostCommentValidator : AbstractValidator<GetPostCommentQuery>
{
    public GetPostCommentValidator() { RuleFor(q => q.PostId).NotEmpty(); RuleFor(q => q.ViewerId).NotEmpty(); }
}

public sealed class GetPostCommentQueryHandler(IPostRepository repository, IUserServiceClient users)
    : IRequestHandler<GetPostCommentQuery, IReadOnlyList<CommentDto>>
{
    public async Task<IReadOnlyList<CommentDto>> Handle(GetPostCommentQuery request, CancellationToken cancellationToken)
    {
        var post = await repository.GetPostByIdAsync(request.PostId, cancellationToken)
            ?? throw new KeyNotFoundException("Post was not found.");

        var comments = await repository.GetCommentsByPostIdAsync(request.PostId, cancellationToken);
        var authors = await users.GetUsersAsync(comments.Select(c => c.UserId), cancellationToken);
        return comments
            .Select(comment => comment.ToCommentDto(
                authors.TryGetValue(comment.UserId, out var user) ? $"{user.FirstName} {user.LastName}".Trim() : PostPresentation.UnknownUser,
                request.ViewerId,
                post.AuthorUserId))
            .ToList();
    }
}
