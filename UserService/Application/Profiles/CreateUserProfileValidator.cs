using FluentValidation;

namespace UserService.Application.Profiles;

public sealed class CreateUserProfileValidator : AbstractValidator<CreateUserProfileCommand>
{
    public CreateUserProfileValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Headline).NotEmpty().MaximumLength(160);
        RuleFor(command => command.About).MaximumLength(2_000);
        RuleFor(command => command.Location).MaximumLength(120);
    }
}
