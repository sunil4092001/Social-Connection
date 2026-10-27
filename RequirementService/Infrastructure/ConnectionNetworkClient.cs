using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuildingBlocks.Response;

namespace RequirementService.Infrastructure;

/// <summary>Network distance between the signed-in user and other users, from ConnectionService.</summary>
public interface IConnectionNetworkClient
{
    /// <summary>
    /// Degree (1 = connected, 2 = connected through someone) for each user in the caller's network.
    /// Users outside the network are not in the result. Returns an empty result when ConnectionService is unavailable,
    /// so matching works without the bonus instead of failing.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetDegreesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}

public sealed class ConnectionNetworkClient(HttpClient httpClient, IHttpContextAccessor contextAccessor, ILogger<ConnectionNetworkClient> logger) : IConnectionNetworkClient
{
    private const int MaxPerCall = 200;

    public async Task<IReadOnlyDictionary<Guid, int>> GetDegreesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().Take(MaxPerCall).ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/connections/degrees") { Content = JsonContent.Create(new { userIds = ids }) };
            var token = contextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("ConnectionService degrees returned {StatusCode}; matching without network bonus", response.StatusCode);
                return new Dictionary<Guid, int>();
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<DegreeDto>>>(cancellationToken: cancellationToken);
            return (result?.Data ?? [])
                .Where(item => item.Degree is 1 or 2)
                .ToDictionary(item => item.UserId, item => item.Degree!.Value);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "ConnectionService unavailable; matching without network bonus");
            return new Dictionary<Guid, int>();
        }
    }

    private sealed record DegreeDto(Guid UserId, int? Degree, int MutualConnectionCount);
}
