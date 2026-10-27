using MediatR;
using UserService.Application.Dtos;
using UserService.Domain;

namespace UserService.Application.Skills;

public sealed record AddProfessionalSkillCommand(Guid UserId, Guid SkillId, int ExperienceYears, SkillLevel SkillLevel) : IRequest<ProfileSkillDto>;
