using FluentValidation;
using MediatR;

namespace PostService.Application.Features.Posts.Commands.DeletePost;

/// <summary>RequestedByUserId comes from the JWT; only the post's author may delete it.</summary>
public sealed record DeletePostCommand(Guid PostId, Guid RequestedByUserId) : IRequest<bool>;

public sealed class DeletePostValidator : AbstractValidator<DeletePostCommand>
{
    public DeletePostValidator()
    {
        RuleFor(command => command.PostId).NotEmpty();
        RuleFor(command => command.RequestedByUserId).NotEmpty();
    }
}
