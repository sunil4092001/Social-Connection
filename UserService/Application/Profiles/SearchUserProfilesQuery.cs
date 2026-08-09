using MediatR;
using UserService.Application.Dtos;

namespace UserService.Application.Profiles;

public sealed record SearchUserProfilesQuery(string? Term) : IRequest<IReadOnlyList<Dtos.UpdateUserProfileCommand>>;
