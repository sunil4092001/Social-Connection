using BuildingBlocks.Exceptions;
using MediatR;
using UserService.Domain;
using UserService.Repositories;

namespace UserService.Application.Skills;

public sealed class RemoveProfessionalSkillCommandHandler(IUserProfileRepository repository)
    : IRequestHandler<RemoveProfessionalSkillCommand, bool>
{
    public async Task<bool> Handle(RemoveProfessionalSkillCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("User profile was not found.");

        if (profile.UserType != UserType.PROFESSIONAL)
        {
            throw new ForbiddenException("Only professional users can manage professional skills.");
        }

        return await repository.RemoveProfessionalSkillAsync(request.UserId, request.SkillId, cancellationToken);
    }
}
