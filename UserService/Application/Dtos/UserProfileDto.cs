namespace UserService.Application.Dtos;

public sealed record UpdateUserProfileCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string Headline,
    string? About,
    string? Location,
    DateTime CreatedAtUtc);
