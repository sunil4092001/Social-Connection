using MediatR;
using UserService.Application.Dtos;
using UserService.Domain;

namespace UserService.Application.Profiles;

public sealed record CreateUserProfileCommand(
    Guid Id, string FirstName, string LastName, string? DisplayName = null,
    string? Headline = null, string? About = null, string? Location = null,
    UserType UserType = UserType.CUSTOMER, ProfessionalType? ProfessionalType = null,
    int? ExperienceYears = null, AvailabilityStatus? AvailabilityStatus = null) : IRequest<UpdateUserProfileCommand>
{
    // Retains compatibility with the original display-name-first command used by existing callers.
    public CreateUserProfileCommand(
        string displayName,
        string firstName,
        string lastName,
        string? headline,
        string? about,
        string? location)
        : this(Guid.Empty, firstName, lastName, displayName, headline, about, location)
    {
    }
}
