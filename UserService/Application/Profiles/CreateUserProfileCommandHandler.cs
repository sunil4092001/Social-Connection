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
            DisplayName = request.DisplayName.Trim(),
            Headline = request.Headline.Trim(),
            About = string.IsNullOrWhiteSpace(request.About) ? null : request.About.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(profile, cancellationToken);
        return profile.ToDto();
    }
}

