using FluentValidation;
using MediatR;
using PostService.Application.Features.Comments.Dtos;

namespace PostService.Application.Features.Comments.Commands.CreateCommand;

/// <summary>AuthorUserId comes from the JWT.</summary>
public sealed record CreateCommentCommand(Guid PostId, Guid AuthorUserId, string Content) : IRequest<CommentDto>;

public sealed class CreateCommentValidator : AbstractValidator<CreateCommentCommand>
{
    public CreateCommentValidator()
    {
        RuleFor(command => command.PostId).NotEmpty();
        RuleFor(command => command.AuthorUserId).NotEmpty();
        RuleFor(command => command.Content).NotEmpty().MaximumLength(1_000);
    }
}
