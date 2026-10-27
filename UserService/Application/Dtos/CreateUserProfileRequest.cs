using UserService.Domain;

namespace UserService.Application.Dtos;

public sealed class CreateUserProfileRequest
{
    public Guid UserId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Headline { get; init; }
    public string? About { get; init; }
    public string? Location { get; init; }
    public UserType UserType { get; init; } = UserType.CUSTOMER;
    public ProfessionalType? ProfessionalType { get; init; }
    public int? ExperienceYears { get; init; }
    public AvailabilityStatus? AvailabilityStatus { get; init; }
}
