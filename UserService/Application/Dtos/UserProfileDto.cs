using UserService.Domain;

namespace UserService.Application.Dtos;

public sealed record UpdateUserProfileCommand(
    Guid Id, 
    string FirstName, 
    string LastName, 
    string DisplayName, 
    string Headline,
    string? About, 
    string? Bio, 
    string? Location, 
    DateTime CreatedAtUtc
    , UserType UserType,
    ProfessionalType? ProfessionalType, 
    int? ExperienceYears, 
    AvailabilityStatus? AvailabilityStatus,
    bool IsVerified, 
    string? ProfileImageUrl, 
    IReadOnlyList<ProfileSkillDto> Skills
    
    );
