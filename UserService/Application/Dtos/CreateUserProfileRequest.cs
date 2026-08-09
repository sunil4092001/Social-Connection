namespace UserService.Application.Dtos;

public sealed record CreateUserProfileRequest(
    Guid UserId,
    string FirstName,
    string LastName,
    string DisplayName,
    string Headline,
    string? About,
    string? Location);
