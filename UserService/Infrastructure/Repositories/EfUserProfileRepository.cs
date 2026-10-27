using Microsoft.EntityFrameworkCore;
using UserService.Application.Dtos.ServiceCall;
using UserService.Domain;
using UserService.Infrastructure.Persistence;
using UserService.Repositories;

namespace UserService.Infrastructure.Repositories;

public sealed class EfUserProfileRepository(UserDbContext dbContext) : IUserProfileRepository
{
    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken)
    {


        dbContext.UserProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken)
    {


        dbContext.UserProfiles.Update(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.UserProfiles
            .Include(profile => profile.ProfessionalSkills)
            .ThenInclude(profileSkill => profileSkill.Skill)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<UserProfile>> SearchAsync(
        string? term,
        ProfessionalType? professionalType,
        string? location,
        string? skill,
        int? minimumExperienceYears,
        AvailabilityStatus? availabilityStatus,
        CancellationToken cancellationToken)
    {
        IQueryable<UserProfile> query = dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserType == UserType.PROFESSIONAL)
            .Include(profile => profile.ProfessionalSkills)
            .ThenInclude(profileSkill => profileSkill.Skill);

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(profile =>
                profile.FirstName.Contains(term) ||
                profile.LastName.Contains(term) ||
                profile.DisplayName.Contains(term) ||
                profile.Headline.Contains(term));
        }

        if (professionalType.HasValue)
            query = query.Where(profile => profile.ProfessionalType == professionalType);
        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(profile => profile.Location != null && profile.Location.ToLower().Contains(location.Trim().ToLower()));
        if (!string.IsNullOrWhiteSpace(skill))
            query = query.Where(profile => profile.ProfessionalSkills.Any(profileSkill => profileSkill.Skill.Name.ToLower().Contains(skill.Trim().ToLower())));
        if (minimumExperienceYears.HasValue)
            query = query.Where(profile => profile.ExperienceYears >= minimumExperienceYears.Value);
        if (availabilityStatus.HasValue)
            query = query.Where(profile => profile.AvailabilityStatus == availabilityStatus);

        return await query.OrderBy(profile => profile.FirstName).Take(50).ToListAsync(cancellationToken);
    }

    public async Task<List<UserSummaryDto>> GetUsersProfileAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        return await dbContext.UserProfiles
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new UserSummaryDto(
                u.UserId,
                u.FirstName,
                u.LastName
                )).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Skill>> GetSkillsAsync(CancellationToken cancellationToken) =>
        await dbContext.Skills.AsNoTracking().OrderBy(skill => skill.Category).ThenBy(skill => skill.Name).ToListAsync(cancellationToken);

    public Task<Skill?> GetSkillByIdAsync(Guid skillId, CancellationToken cancellationToken) =>
        dbContext.Skills.FirstOrDefaultAsync(skill => skill.Id == skillId, cancellationToken);

    public async Task AddSkillAsync(Skill skill, CancellationToken cancellationToken)
    {
        dbContext.Skills.Add(skill);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddProfessionalSkillAsync(ProfessionalSkill professionalSkill, CancellationToken cancellationToken)
    {
        dbContext.ProfessionalSkills.Add(professionalSkill);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveProfessionalSkillAsync(Guid professionalId, Guid skillId, CancellationToken cancellationToken)
    {
        var professionalSkill = await dbContext.ProfessionalSkills.FindAsync([professionalId, skillId], cancellationToken);
        if (professionalSkill is null)
        {
            return false;
        }

        dbContext.ProfessionalSkills.Remove(professionalSkill);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

}
