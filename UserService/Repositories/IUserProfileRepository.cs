using UserService.Application.Dtos.ServiceCall;
using UserService.Domain;

namespace UserService.Repositories;

public interface IUserProfileRepository
{
    Task AddAsync(UserProfile profile, CancellationToken cancellationToken);
    Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken);
    Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserProfile>> SearchAsync(
        string? term,
        ProfessionalType? professionalType,
        string? location,
        string? skill,
        int? minimumExperienceYears,
        AvailabilityStatus? availabilityStatus,
        CancellationToken cancellationToken);
    Task<List<UserSummaryDto>> GetUsersProfileAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<Skill>> GetSkillsAsync(CancellationToken cancellationToken);
    Task<Skill?> GetSkillByIdAsync(Guid skillId, CancellationToken cancellationToken);
    Task AddSkillAsync(Skill skill, CancellationToken cancellationToken);
    Task AddProfessionalSkillAsync(ProfessionalSkill professionalSkill, CancellationToken cancellationToken);
    Task<bool> RemoveProfessionalSkillAsync(Guid professionalId, Guid skillId, CancellationToken cancellationToken);
}
