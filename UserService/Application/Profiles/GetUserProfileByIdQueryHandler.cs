using MediatR;
using UserService.Application.Dtos;
using UserService.Application.Mapping;
using UserService.Repositories;

namespace UserService.Application.Profiles;

public sealed class GetUserProfileByIdQueryHandler(IUserProfileRepository repository)
    : IRequestHandler<GetUserProfileByIdQuery, Dtos.UpdateUserProfileCommand?>
{
    public async Task<Dtos.UpdateUserProfileCommand?> Handle(GetUserProfileByIdQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetByIdAsync(request.Id, cancellationToken);
        return profile?.ToDto();
    }
}
