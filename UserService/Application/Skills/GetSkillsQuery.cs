using MediatR;
using UserService.Application.Dtos;

namespace UserService.Application.Skills;

public sealed record GetSkillsQuery : IRequest<IReadOnlyList<SkillDto>>;
