namespace PostService.Application.Features.Posts.DTOs.FeedPost
{
    public sealed record FeedPostDto    
    (
        Guid Id,
        Guid authorUserId,
        string AuthorName,
        string Content,
        DateTime CreatedAt
    );
}
