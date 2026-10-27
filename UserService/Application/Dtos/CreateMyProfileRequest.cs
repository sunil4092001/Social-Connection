using UserService.Domain;

namespace UserService.Application.Dtos;

public sealed record CreateMyProfileRequest(
    string FirstName,
    string LastName,
    string? DisplayName,
    string? Headline,
    string? About,
    string? Location,
    UserType UserType,
    ProfessionalType? ProfessionalType,
    int? ExperienceYears,
    AvailabilityStatus? AvailabilityStatus);