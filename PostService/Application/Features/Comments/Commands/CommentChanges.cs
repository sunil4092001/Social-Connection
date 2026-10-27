using BuildingBlocks.Exceptions;
using FluentValidation;
using MediatR;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Posts;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Comments.Commands;

// ----- Edit (comment author only) -----

public sealed record UpdateCommentCommand(Guid CommentId, Guid UserId, string Content) : IRequest<CommentDto>;

public sealed class UpdateCommentValidator : AbstractValidator<UpdateCommentCommand>
{
    public UpdateCommentValidator()
    {
        RuleFor(c => c.CommentId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Content).NotEmpty().MaximumLength(1_000);
    }
}

public sealed class UpdateCommentCommandHandler(IPostRepository repository, IUserServiceClient users) : IRequestHandler<UpdateCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await repository.GetCommentByIdAsync(request.CommentId, cancellationToken)
            ?? throw new KeyNotFoundException("Comment was not found.");
        if (comment.UserId != request.UserId)
            throw new ForbiddenException("Only the author can edit this comment.");

        comment.Content = request.Content.Trim();
        comment.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        var name = await PostPresentation.NameOfAsync(users, comment.UserId, cancellationToken);
        return comment.ToCommentDto(name, request.UserId, comment.Posts.AuthorUserId);
    }
}

// ----- Delete (comment author or post author) -----

public sealed record DeleteCommentCommand(Guid CommentId, Guid UserId) : IRequest;

public sealed class DeleteCommentValidator : AbstractValidator<DeleteCommentCommand>
{
    public DeleteCommentValidator() { RuleFor(c => c.CommentId).NotEmpty(); RuleFor(c => c.UserId).NotEmpty(); }
}

public sealed class DeleteCommentCommandHandler(IPostRepository repository) : IRequestHandler<DeleteCommentCommand>
{
    public async Task Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await repository.GetCommentByIdAsync(request.CommentId, cancellationToken)
            ?? throw new KeyNotFoundException("Comment was not found.");
        // A post author can moderate the discussion under their own post.
        if (comment.UserId != request.UserId && comment.Posts.AuthorUserId != request.UserId)
            throw new ForbiddenException("Only the comment author or the post author can delete this comment.");

        repository.RemoveComment(comment);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
