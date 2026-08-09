using MediatR;
using PostService.Application.Features.Comments.Dtos;

namespace PostService.Application.Features.Comments.Commands.CreateCommand
{
    public sealed record CreateCommentCommand
    (
        Guid PostId,
        Guid AuthorUserId,
        //string AuthorName,
        //string? AuthorProfileImage,
        string Content
    ) : IRequest<CommentDto>;
    
}
