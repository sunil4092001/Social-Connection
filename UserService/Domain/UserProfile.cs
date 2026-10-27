namespace UserService.Domain;

/// <summary>
/// Profile data owned by UserService. Authentication data deliberately remains in IdentityService.
/// </summary>
public sealed class UserProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string? About { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Company { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Website { get; set; }
    public UserType UserType { get; set; } = UserType.CUSTOMER;
    public ProfessionalType? ProfessionalType { get; set; }
    public int? ExperienceYears { get; set; }
    public AvailabilityStatus? AvailabilityStatus { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProfessionalSkill> ProfessionalSkills { get; set; } = new List<ProfessionalSkill>();
}
