using UserService.Application.Dtos;
using UserService.Domain;

namespace UserService.Application.Mapping;

public static class UserProfileMapping
{
    public static UpdateUserProfileCommand ToDto(this UserProfile profile) =>
        new(
            profile.UserId,
            profile.FirstName,
            profile.LastName,
            profile.DisplayName,
            profile.Headline,
            profile.About,
            profile.Bio,
            profile.Location,
            profile.CreatedAtUtc,
            profile.UserType,
            profile.ProfessionalType,
            profile.ExperienceYears,
            profile.AvailabilityStatus,
            profile.IsVerified,
            profile.ProfileImageUrl,
            profile.ProfessionalSkills
                .OrderBy(x => x.Skill.Name)
                .Select(x => new ProfileSkillDto(x.SkillId, x.Skill.Name, x.Skill.Category, x.ExperienceYears, x.SkillLevel))
                .ToList());
}
