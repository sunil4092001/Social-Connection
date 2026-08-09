using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts.Client
{
    public class UserServiceClient : IUserServiceClient
    {
        private readonly HttpClient _httpClient;

        public UserServiceClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<Dictionary<Guid, UserSummaryDto>> GetUsersAsync(IEnumerable<Guid> autherIds, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync(
            "/api/users/batch",
            autherIds, 
            cancellationToken);

            response.EnsureSuccessStatusCode();

            var users = await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>(cancellationToken: cancellationToken);

            return users!.ToDictionary(x => x.Id);

        }
    }
}
