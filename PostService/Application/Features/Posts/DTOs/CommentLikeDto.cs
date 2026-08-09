namespace PostService.Application.Features.Posts.DTOs
{
    public sealed record CommentLikeDto
        (
            Guid CommentLikeId,
            Guid UserId,
            DateTime CreatedAt
        );
}
