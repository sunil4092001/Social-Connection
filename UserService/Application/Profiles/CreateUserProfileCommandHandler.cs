using System.Security.Claims;
using MediatR;
using UserService.Application.Dtos;
using UserService.Application.Mapping;
using UserService.Domain;
using UserService.Repositories;

namespace UserService.Application.Profiles;

public sealed class CreateUserProfileCommandHandler(IUserProfileRepository repository)
    : IRequestHandler<CreateUserProfileCommand, Dtos.UpdateUserProfileCommand>
{
    public async Task<Dtos.UpdateUserProfileCommand> Handle(CreateUserProfileCommand request, CancellationToken cancellationToken)
    {
       // var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var profile = new UserProfile
        {
            Id = request.Id,
            UserId = request.Id,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? $"{request.FirstName} {request.LastName}".Trim() : request.DisplayName.Trim(),
            Headline = string.IsNullOrWhiteSpace(request.Headline) ? "BuildConnect professional" : request.Headline.Trim(),
            About = string.IsNullOrWhiteSpace(request.About) ? null : request.About.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            UserType = request.UserType,
            ProfessionalType = request.ProfessionalType,
            ExperienceYears = request.ExperienceYears,
            AvailabilityStatus = request.AvailabilityStatus,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(profile, cancellationToken);
        return profile.ToDto();
    }
}
