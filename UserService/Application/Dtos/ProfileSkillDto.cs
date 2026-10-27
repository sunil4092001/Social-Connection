using UserService.Domain;

namespace UserService.Application.Dtos;

public sealed record ProfileSkillDto(Guid SkillId, string Name, string Category, int ExperienceYears, SkillLevel SkillLevel);
