namespace IdentityService.DTOs
{
    public class RegisterRequest
    {
        public string Email { get; set; }

        public string Password { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        // Profile data is forwarded to UserService and is never persisted in IdentityDb.
        public string UserType { get; set; } = "CUSTOMER";
        public string? ProfessionalType { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Location { get; set; }
    }
}
