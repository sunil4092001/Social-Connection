using PostService.Application.Features.Comments.Dtos;

namespace PostService.Application.Features.Posts.DTOs
{
    public sealed record PostDetailsDto
    (
        Guid PostId,
        Guid AuthorUserId,
        string Content,
        string? ImageUrl,
        DateTime CreatedAt,
        string? Visibility,
        IReadOnlyList<CommentDto> Comments,
        IReadOnlyList<PostLikeDto> Likes    
    );
    
}
