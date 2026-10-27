using BuildingBlocks.Exceptions;
using MediatR;
using UserService.Application.Dtos;
using UserService.Domain;
using UserService.Repositories;

namespace UserService.Application.Skills;

public sealed class AddProfessionalSkillCommandHandler(IUserProfileRepository repository)
    : IRequestHandler<AddProfessionalSkillCommand, ProfileSkillDto>
{
    public async Task<ProfileSkillDto> Handle(AddProfessionalSkillCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("User profile was not found.");

        if (profile.UserType != UserType.PROFESSIONAL)
        {
            throw new ForbiddenException("Only professional users can manage professional skills.");
        }

        var skill = await repository.GetSkillByIdAsync(request.SkillId, cancellationToken)
            ?? throw new KeyNotFoundException("Skill was not found.");

        if (profile.ProfessionalSkills.Any(x => x.SkillId == request.SkillId))
        {
            throw new ConflictException("This skill is already assigned to the professional.");
        }

        await repository.AddProfessionalSkillAsync(new ProfessionalSkill
        {
            ProfessionalId = request.UserId,
            SkillId = request.SkillId,
            ExperienceYears = request.ExperienceYears,
            SkillLevel = request.SkillLevel
        }, cancellationToken);

        return new ProfileSkillDto(skill.Id, skill.Name, skill.Category, request.ExperienceYears, request.SkillLevel);
    }
}
