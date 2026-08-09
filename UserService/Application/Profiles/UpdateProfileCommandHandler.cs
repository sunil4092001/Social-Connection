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
            var profile = new UserProfile
            {
                Id = request.UserId,
                UserId = request.UserId,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                DisplayName = request.DisplayName.Trim(),
                Headline = request.Headline.Trim(),
                About = string.IsNullOrWhiteSpace(request.About) ? null : request.About.Trim(),
                Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            await repository.UpdateAsync(profile, cancellationToken);
            return profile.ToDto();
        }
    }
}
