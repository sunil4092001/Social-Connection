using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Posts.Mapping
{
    public static class PostMapping
    {
        public static PostDto ToDto(this Post post) =>
       new(post.PostId, post.AuthorUserId, post.Content, post.CreatedAt);

        public static FeedPostDto ToFeedDto(
        this Post post,
        UserSummaryDto? user)
        {
            return new FeedPostDto(
                post.PostId,
                post.AuthorUserId,
                user == null
                    ? "Unknown User"
                    : $"{user.FirstName} {user.LastName}",
                post.Content,
                post.CreatedAt);
        }
    }
}
