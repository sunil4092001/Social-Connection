using MediatR;
using UserService.Application.Dtos;

namespace UserService.Application.Profiles;

public sealed record GetUserProfileByIdQuery(Guid Id) : IRequest<Dtos.UpdateUserProfileCommand?>;
