using UserService.Application.Dtos;
using UserService.Domain;

namespace UserService.Application.Mapping;

public static class UserProfileMapping
{
    public static UpdateUserProfileCommand ToDto(this UserProfile profile) =>
        new(profile.Id, profile.FirstName, profile.LastName, profile.DisplayName, profile.Headline, profile.About, profile.Location, profile.CreatedAtUtc);
}
