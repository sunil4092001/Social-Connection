using FluentValidation;
using MediatR;
using PostService.Application.Features.Posts.DTOs;

namespace PostService.Application.Features.Posts.Commands.UpdatePost;

/// <summary>EditorUserId comes from the JWT; only the post's author may edit it.</summary>
public sealed record UpdatePostCommand(
    Guid PostId,
    Guid EditorUserId,
    string Content,
    string? ImageUrl,
    string? Visibility) : IRequest<PostDto?>;

public sealed class UpdatePostValidator : AbstractValidator<UpdatePostCommand>
{
    public UpdatePostValidator()
    {
        RuleFor(command => command.PostId).NotEmpty();
        RuleFor(command => command.EditorUserId).NotEmpty();
        RuleFor(command => command.Content).NotEmpty().MaximumLength(3_000);
        RuleFor(command => command.ImageUrl).MaximumLength(1_000);
        RuleFor(command => command.Visibility).MaximumLength(20);
    }
}
