using System.Numerics;

namespace PostService.Application.Features.Posts.DTOs
{
    public sealed record PostSummaryDto
    (
        Guid PostId,
        Guid AuthorUserId,
        string Content,
        string? ImageUrl,
        DateTime CreatedAt,
        string? Visibility
        //BigInteger CommentCount,
        //BigInteger LikeCount
    );
    
}
