using MediatR;
using UserService.Application.Dtos;
using UserService.Repositories;

namespace UserService.Application.Skills;

public sealed class GetSkillsQueryHandler(IUserProfileRepository repository) : IRequestHandler<GetSkillsQuery, IReadOnlyList<SkillDto>>
{
    public async Task<IReadOnlyList<SkillDto>> Handle(GetSkillsQuery request, CancellationToken cancellationToken) =>
        (await repository.GetSkillsAsync(cancellationToken))
            .Select(skill => new SkillDto(skill.Id, skill.Name, skill.Category))
            .ToList();
}
