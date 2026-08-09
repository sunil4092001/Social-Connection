namespace IdentityService.DTOs.User
{
    public class RegisterResponse
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
