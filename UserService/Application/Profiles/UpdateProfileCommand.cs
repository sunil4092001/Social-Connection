using MediatR;

namespace UserService.Application.Profiles
{
    public sealed record UpdateProfileCommand
    (
        Guid UserId,
        string FirstName,
        string LastName,
        string DisplayName,
        string Headline,
        string? About,
        string? Location
        ) : IRequest<Dtos.UpdateUserProfileCommand>;

}
