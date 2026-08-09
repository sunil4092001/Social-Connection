using PostService.Application.Features.Posts.DTOs.FeedPost;

namespace PostService.Application.Interfaces
{
    public interface IUserServiceClient
    {
        Task<Dictionary<Guid, UserSummaryDto>> GetUsersAsync(
        IEnumerable<Guid> autherIds,
        CancellationToken cancellationToken);
    }
}
