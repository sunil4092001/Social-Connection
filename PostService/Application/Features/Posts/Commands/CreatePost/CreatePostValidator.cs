using FluentValidation;
using MediatR;

namespace PostService.Application.Features.Posts.Commands.CreatePost
{

    //public sealed record CreatePostCommand(Guid AuthorUserId, string Content) : IRequest<PostDto>;

    public class CreatePostValidator : AbstractValidator<CreatePostCommand>
    {
        public CreatePostValidator()
        {
            RuleFor(command => command.AuthorUserId).NotEmpty();
            RuleFor(command => command.Content).NotEmpty().MaximumLength(3_000);
        }
    }
}
