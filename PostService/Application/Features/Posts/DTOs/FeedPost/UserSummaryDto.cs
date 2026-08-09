namespace PostService.Application.Features.Posts.DTOs.FeedPost
{
    public class UserSummaryDto
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;
    }
}
