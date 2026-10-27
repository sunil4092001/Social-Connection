using ConnectionService.Application.DTOs;

namespace ConnectionService.Application.Interfaces;

/// <summary>
/// The connection graph. Each write runs in a single Neo4j transaction that also stores its outbox event,
/// so a saved change always produces its event.
/// </summary>
public interface IConnectionRepository
{
    /// <summary>Creates (or re-opens a rejected) request. Throws ConflictException when the rules forbid it.</summary>
    Task<ConnectionDto> RequestAsync(Guid requesterUserId, Guid addresseeUserId, OutboxEnvelope @event, CancellationToken cancellationToken);

    /// <summary>Turns the pending request requester -> addressee into a connection. Null when there is no pending request.</summary>
    Task<ConnectionDto?> AcceptAsync(Guid requesterUserId, Guid addresseeUserId, OutboxEnvelope @event, CancellationToken cancellationToken);

    /// <summary>Marks the pending request requester -> addressee as rejected. False when there is no pending request.</summary>
    Task<bool> RejectAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken);

    /// <summary>Deletes my pending request to the addressee. False when there is none.</summary>
    Task<bool> WithdrawAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken);

    /// <summary>Removes an existing connection. False when the users are not connected.</summary>
    Task<bool> RemoveAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectionEdge>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectionEdge>> GetPendingReceivedAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectionEdge>> GetPendingSentAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetMutualConnectionsAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
    Task<ConnectionStatusDto> GetStatusAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);

    /// <summary>Friends of friends I am not connected to and have no request with, most mutual connections first.</summary>
    Task<IReadOnlyList<SuggestionEdge>> GetSuggestionsAsync(Guid userId, int limit, CancellationToken cancellationToken);

    /// <summary>Distance (1 or 2) from <paramref name="userId"/> to each candidate; candidates further away are left out.</summary>
    Task<IReadOnlyList<NetworkDegreeDto>> GetDegreesAsync(Guid userId, IReadOnlyCollection<Guid> candidateIds, CancellationToken cancellationToken);
}
