using MediatR;
using UserService.Application.Dtos;

namespace UserService.Application.Profiles;

public sealed record CreateUserProfileCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string Headline,
    string? About,
    string? Location) : IRequest<Dtos.UpdateUserProfileCommand>;
