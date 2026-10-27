using FluentValidation;

namespace UserService.Application.Skills;

public sealed class AddProfessionalSkillValidator : AbstractValidator<AddProfessionalSkillCommand>
{
    public AddProfessionalSkillValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.SkillId).NotEmpty();
        RuleFor(command => command.ExperienceYears).InclusiveBetween(0, 80);
    }
}
