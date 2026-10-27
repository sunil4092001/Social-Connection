using System.Net.Http.Headers;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Interfaces;

namespace PostService.Infrastructure.Clients;

/// <summary>
/// Author names from UserService. Forwards the caller's JWT (the batch endpoint requires a signed-in user) and
/// returns an empty result on failure, so the feed still loads when UserService is down.
/// </summary>
public class UserServiceClient(HttpClient httpClient, IHttpContextAccessor contextAccessor, ILogger<UserServiceClient> logger) : IUserServiceClient
{
    public async Task<Dictionary<Guid, UserSummaryDto>> GetUsersAsync(IEnumerable<Guid> autherIds, CancellationToken cancellationToken)
    {
        var ids = autherIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/users/batch") { Content = JsonContent.Create(ids) };
            ForwardToken.Apply(request, contextAccessor);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("UserService batch lookup returned {StatusCode}", response.StatusCode);
                return [];
            }

            var users = await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>(cancellationToken: cancellationToken) ?? [];
            return users.GroupBy(user => user.Id).ToDictionary(group => group.Key, group => group.First());
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "UserService batch lookup failed");
            return [];
        }
    }
}

internal static class ForwardToken
{
    /// <summary>Calls to other services run as the signed-in user, so they apply their own authorization.</summary>
    public static void Apply(HttpRequestMessage request, IHttpContextAccessor contextAccessor)
    {
        var token = contextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
    }
}
