using MediatR;
using PostService.Application.Features.Posts.DTOs;

namespace PostService.Application.Features.Posts.Queries
{
    public sealed record GetPostByIdQuery(Guid PostId) : IRequest<PostSummaryDto>;
    
}
