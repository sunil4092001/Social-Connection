using MediatR;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Features.Posts.DTOs.FeedPost;

namespace PostService.Application.Features.Posts.Queries
{
    public sealed record GetFeedQuery(int Page, int PageSize) : IRequest<IReadOnlyList<FeedPostDto>>

    {
        // use record
        //public sealed record GetFeedQuery(int Page, int PageSize) : IRequest<IReadOnlyList<PostDto>>;

    }

}
