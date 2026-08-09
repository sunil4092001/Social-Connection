namespace PostService.Application.Features.Posts.Request
{
    public sealed record UpdatePostRequest
    (
        string Content,
        string ImageUrl,
        string Visibility
        
    );
}
