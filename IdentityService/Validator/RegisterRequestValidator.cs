using FluentValidation;
using IdentityService.DTOs;

namespace IdentityService.Configuration
{
    public class RegisterRequestValidator :AbstractValidator<RegisterRequest>

    {
        public RegisterRequestValidator()
        {
            RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format");

            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Password is required")
                .MinimumLength(6)
                .WithMessage("Password must be at least 6 characters");

            RuleFor(x => x.FirstName)
                .NotEmpty()
                .WithMessage("First name is required");

            RuleFor(x => x.LastName)
                .NotEmpty()
                .WithMessage("Last name is required")
                ;

            RuleFor(x => x.UserType)
                .Must(type => string.Equals(type, "CUSTOMER", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "PROFESSIONAL", StringComparison.OrdinalIgnoreCase))
                .WithMessage("User type must be CUSTOMER or PROFESSIONAL.");

            RuleFor(x => x.ProfessionalType)
                .NotEmpty()
                .When(x => string.Equals(x.UserType, "PROFESSIONAL", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Professional type is required for professional users.");

            RuleFor(x => x.ExperienceYears)
                .NotNull().GreaterThanOrEqualTo(0)
                .When(x => string.Equals(x.UserType, "PROFESSIONAL", StringComparison.OrdinalIgnoreCase));
        }


    }
}
