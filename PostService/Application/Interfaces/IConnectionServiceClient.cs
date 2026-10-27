namespace PostService.Application.Interfaces;

/// <summary>Reads the signed-in user's network from ConnectionService (PostService never touches the Neo4j graph).</summary>
public interface IConnectionServiceClient
{
    /// <summary>Ids of the caller's connections, or null when ConnectionService cannot be reached.</summary>
    Task<IReadOnlyList<Guid>?> GetMyConnectionIdsAsync(CancellationToken cancellationToken);
}
