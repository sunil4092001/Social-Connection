using MediatR;
using UserService.Application.Dtos;
using UserService.Application.Mapping;
using UserService.Repositories;

namespace UserService.Application.Profiles;

public sealed class SearchUserProfilesQueryHandler(IUserProfileRepository repository)
    : IRequestHandler<SearchUserProfilesQuery, IReadOnlyList<Dtos.UpdateUserProfileCommand>>
{
    public async Task<IReadOnlyList<Dtos.UpdateUserProfileCommand>> Handle(SearchUserProfilesQuery request, CancellationToken cancellationToken)
    {
        var profiles = await repository.SearchAsync(
            request.Term,
            request.ProfessionalType,
            request.Location,
            request.Skill,
            request.MinimumExperienceYears,
            request.AvailabilityStatus,
            cancellationToken);
        return profiles.Select(profile => profile.ToDto()).ToList();
    }
}
