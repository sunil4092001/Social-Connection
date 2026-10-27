using BuildingBlocks.Response;
using PostService.Application.Interfaces;

namespace PostService.Infrastructure.Clients;

/// <summary>Asks ConnectionService for the caller's connections (GET /api/connections/me) to build the network feed.</summary>
public sealed class ConnectionServiceClient(HttpClient httpClient, IHttpContextAccessor contextAccessor, ILogger<ConnectionServiceClient> logger) : IConnectionServiceClient
{
    public async Task<IReadOnlyList<Guid>?> GetMyConnectionIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/connections/me");
            ForwardToken.Apply(request, contextAccessor);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("ConnectionService returned {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ConnectionUser>>>(cancellationToken: cancellationToken);
            return result?.Data?.Select(connection => connection.UserId).ToList() ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "ConnectionService is unreachable");
            return null;
        }
    }

    private sealed record ConnectionUser(Guid UserId);
}
