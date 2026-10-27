using MediatR;

namespace UserService.Application.Skills;

public sealed record RemoveProfessionalSkillCommand(Guid UserId, Guid SkillId) : IRequest<bool>;
