using MediatR;
using PostService.Application.Features.Comments.Dtos;

namespace PostService.Application.Features.Comments.Queries
{
    public sealed record GetPostCommentQuery
    (
        Guid PostId 
    ) : IRequest<IReadOnlyList<CommentDto>>;

}
