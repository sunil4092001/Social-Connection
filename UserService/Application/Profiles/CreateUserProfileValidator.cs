using FluentValidation;

namespace UserService.Application.Profiles;

public sealed class CreateUserProfileValidator : AbstractValidator<CreateUserProfileCommand>
{
    public CreateUserProfileValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DisplayName).MaximumLength(120);
        RuleFor(command => command.Headline).MaximumLength(160);
        RuleFor(command => command.About).MaximumLength(2_000);
        RuleFor(command => command.Location).MaximumLength(120);
        RuleFor(command => command.ExperienceYears).GreaterThanOrEqualTo(0).When(command => command.ExperienceYears.HasValue);
        RuleFor(command => command.ProfessionalType).NotNull()
            .When(command => command.UserType == Domain.UserType.PROFESSIONAL)
            .WithMessage("Professional type is required for professional users.");
        RuleFor(command => command.ExperienceYears).NotNull()
            .When(command => command.UserType == Domain.UserType.PROFESSIONAL)
            .WithMessage("Experience years is required for professional users.");
    }
}
