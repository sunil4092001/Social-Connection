using MediatR;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Comments.Queries
{
    public sealed class GetPostCommentQueryHandler(IPostRepository repository, IUserServiceClient _userServiceClient) : IRequestHandler<GetPostCommentQuery, IReadOnlyList<CommentDto>>
    {
        public async Task<IReadOnlyList<CommentDto>> Handle(GetPostCommentQuery request, CancellationToken cancellationToken)
        {
            //Will Return List of Post Comment

            //var page = Math.Max(request.Page, 1);
            //var pageSize = Math.Clamp(request.PageSize, 1, 50);
            var comments = await repository.GetCommentsByPostIdAsync(request.PostId, cancellationToken);

            var userIds = comments
                .Select(c => c.UserId)
                .Distinct()
                .ToList();
            var users = await _userServiceClient.GetUsersAsync(userIds, cancellationToken);

            //var userLookup = users.ToDictionary(u => u.Key);

            return comments.Select( comment =>
                {
                    //var user = userLookup[comment.UserId];
                    users.TryGetValue(comment.UserId, out var user);
                    //return new CommentDto(
                    //    comment.CommentId,
                    //    comment.PostId,
                    //    comment.UserId,
                    //    user == null
                    //        ? "Unknown user"
                    //        : $"{user.FirstName} {user.LastName}",
                    //    comment.Content,
                    //    comment.CreatedAt
                    //    );

                    return comment.ToCommentDto(user == null? "Unknown User": $"{user.FirstName} {user.LastName}");

                }).ToList();


            //return comments
            //    .Select(c => c.ToCommentDto())
            //    .ToList();


        }
    }
}
