using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace IdentityService.DTOs.ServiceCall
{
    public class CreateUserProfileRequest
    {
        public CreateUserProfileRequest()
        {
            Location = "Not specified";
            Headline = "Headline";
            About = "About you";
        }
       
        public Guid UserId { get; set; }

        public string FirstName { get; set; } = default!;

        public string LastName { get; set; } = default!;

        public string DisplayName { get; set; } = default!;

        public string Headline { get; set; } = default!;

        public string? About { get; set; }

        public string? Location { get; set; }
    }
}
