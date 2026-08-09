namespace UserService.Application.Dtos.ServiceCall
{
    public class UserSummaryDto
    {
        public UserSummaryDto(Guid id, string firstName, string lastName)
        {
            Id = id;
            FirstName = firstName;
            LastName = lastName;
        }

        public Guid Id { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;
    }
}
