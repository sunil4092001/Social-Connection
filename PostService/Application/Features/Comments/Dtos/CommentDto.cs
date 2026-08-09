using System.Numerics;
using PostService.Application.Features.Posts.DTOs;

namespace PostService.Application.Features.Comments.Dtos
{
    public sealed record CommentDto
    (
        Guid CommentId,
         Guid PostId,
        Guid UserId,
        string UserName,
        //string ProfileImageUrl,
        string Content,
        DateTime CreatedAt
        //bool IsEdited,
       // IReadOnlyList<CommentLikeDto> CommentLikes
        //BigInteger CommentLikeCount
    );
        
}
