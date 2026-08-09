using MediatR;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Features.Posts.Mapping;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts.Queries
{
    public sealed class GetFeedQueryHandler(IPostRepository repository, IUserServiceClient _userServiceClient) : IRequestHandler<GetFeedQuery, IReadOnlyList<FeedPostDto>>
    {

        //public async Task<IReadOnlyList<PostDto>> Handle(GetFeedQuery request, CancellationToken cancellationToken)
        public async Task<IReadOnlyList<FeedPostDto>> Handle(GetFeedQuery request, CancellationToken cancellationToken)
        {
            var page = Math.Max(request.Page, 1);
            var pageSize = Math.Clamp(request.PageSize, 1, 50);
            var posts = await repository.GetFeedAsync((page - 1) * pageSize, pageSize, cancellationToken);

            // calling insternal api
            var userIDs = posts.Select(x => x.AuthorUserId).Distinct().ToList();
            var users = await _userServiceClient.GetUsersAsync(userIDs, cancellationToken);
            //return posts.Select(post => post.ToDto()).ToList();
            return posts.Select(post => 
            {
                //var user = users[post.AuthorUserId];
                users.TryGetValue(post.AuthorUserId, out var user);

                return new FeedPostDto(
                    post.PostId,
                    post.AuthorUserId,
                    user == null 
                        ? "Unknown user"
                        : $"{user.FirstName} {user.LastName}",
                    post.Content,
                    post.CreatedAt
                    );

            }).ToList();
        }
    }
}
