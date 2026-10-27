using UserService.Domain;

namespace UserService.Application.Dtos;

public sealed record AddProfessionalSkillRequest(Guid SkillId, int ExperienceYears, SkillLevel SkillLevel);
