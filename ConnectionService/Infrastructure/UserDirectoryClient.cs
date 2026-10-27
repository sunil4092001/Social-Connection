using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Response;
using ConnectionService.Application.Interfaces;

namespace ConnectionService.Infrastructure;

/// <summary>Calls UserService over HTTP, forwarding the caller's JWT. ConnectionService never reads UserService's database.</summary>
public sealed class UserDirectoryClient(HttpClient httpClient, IHttpContextAccessor contextAccessor, ILogger<UserDirectoryClient> logger) : IUserDirectoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/users/{userId}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        // Anything else unexpected (UserService down, 5xx) is an error: we cannot tell whether the user exists.
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserProfileDto>>(JsonOptions, cancellationToken);
        return result?.Data is { } profile ? DisplayName(profile.DisplayName, profile.FirstName, profile.LastName) : null;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        try
        {
            using var request = CreateRequest(HttpMethod.Post, "api/users/batch");
            request.Content = JsonContent.Create(ids);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("UserService batch lookup returned {StatusCode}", response.StatusCode);
                return new Dictionary<Guid, string>();
            }

            var users = await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>(JsonOptions, cancellationToken) ?? [];
            return users.GroupBy(user => user.Id).ToDictionary(group => group.Key, group => DisplayName(null, group.First().FirstName, group.First().LastName));
        }
        catch (HttpRequestException exception)
        {
            // Names are decoration for lists; the lists still work without them.
            logger.LogWarning(exception, "UserService batch lookup failed");
            return new Dictionary<Guid, string>();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        var token = contextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        return request;
    }

    private static string DisplayName(string? displayName, string? firstName, string? lastName) =>
        !string.IsNullOrWhiteSpace(displayName) ? displayName : $"{firstName} {lastName}".Trim();

    private sealed record UserProfileDto(string? DisplayName, string? FirstName, string? LastName);
    private sealed record UserSummaryDto(Guid Id, string? FirstName, string? LastName);
}
