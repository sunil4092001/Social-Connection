using MediatR;
using UserService.Application.Dtos;
using UserService.Application.Mapping;
using UserService.Domain;
using UserService.Repositories;

namespace UserService.Application.Profiles
{
    public sealed class UpdateProfileCommandHandler(IUserProfileRepository repository) : IRequestHandler<UpdateProfileCommand, Dtos.UpdateUserProfileCommand>
    {
        public async Task<UpdateUserProfileCommand> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
        {
            var profile = await repository.GetByUserIdAsync(request.UserId, cancellationToken)
                ?? throw new KeyNotFoundException("User profile was not found.");

            profile.FirstName = request.FirstName.Trim();
            profile.LastName = request.LastName.Trim();
            profile.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? $"{profile.FirstName} {profile.LastName}" : request.DisplayName.Trim();
            profile.Headline = string.IsNullOrWhiteSpace(request.Headline) ? profile.Headline : request.Headline.Trim();
            profile.About = string.IsNullOrWhiteSpace(request.About) ? null : request.About.Trim();
            profile.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
            profile.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? null : request.ProfileImageUrl.Trim();
            profile.ProfessionalType = profile.UserType == UserType.PROFESSIONAL ? request.ProfessionalType : null;
            profile.ExperienceYears = profile.UserType == UserType.PROFESSIONAL ? request.ExperienceYears : null;
            profile.AvailabilityStatus = profile.UserType == UserType.PROFESSIONAL ? request.AvailabilityStatus : null;
            profile.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(profile, cancellationToken);
            return profile.ToDto();
        }
    }
}
