namespace PostService.Application.Features.Posts.DTOs
{
    public sealed record PostLikeDto
    (
        Guid PostLikeId,
        Guid UserId,
        DateTime CreatedAt);


}
