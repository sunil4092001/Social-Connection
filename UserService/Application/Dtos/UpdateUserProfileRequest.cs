using UserService.Domain;

namespace UserService.Application.Dtos;

public sealed record UpdateUserProfileRequest(
    string FirstName, 
    string LastName, 
    string? DisplayName, 
    string? Headline, 
    string? About,
    string? Location, 
    ProfessionalType? ProfessionalType, 
    int? ExperienceYears,
    AvailabilityStatus? AvailabilityStatus, 
    string? ProfileImageUrl
    
    );
