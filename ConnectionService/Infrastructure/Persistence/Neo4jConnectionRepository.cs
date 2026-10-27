using ConnectionService.Application;
using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using ConnectionService.Domain;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ConnectionService.Infrastructure.Persistence;

/// <summary>
/// Graph model (only ids are stored; names belong to UserService):
///   (:User {id})-[:REQUESTED {status: PENDING|REJECTED, createdAtUtc, respondedAtUtc}]->(:User)   direction = who asked whom
///   (:User)-[:CONNECTED_WITH {requestedAtUtc, createdAtUtc}]-(:User)                              one relationship, queried without direction
///   (:OutboxMessage {id, topic, key, payload, occurredAtUtc, processedAtUtc, attempts, lastError}) events waiting for Kafka
/// </summary>
public sealed class Neo4jConnectionRepository(IDriver driver, IOptions<Neo4jOptions> options) : IConnectionRepository
{
    // Taking a write lock on both users first serialises concurrent requests between the same pair
    // (for example A->B and B->A at the same moment), so the state check below cannot be raced.
    private const string LockPair = """
        SET a._lock = true, b._lock = true
        REMOVE a._lock, b._lock
        """;

    private const string CreateOutboxMessage = """
        CREATE (:OutboxMessage {id: $outboxId, topic: $topic, key: $key, payload: $payload, occurredAtUtc: $now, attempts: 0, traceParent: $traceParent})
        """;

    public async Task<ConnectionDto> RequestAsync(Guid requesterUserId, Guid addresseeUserId, OutboxEnvelope @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = OpenSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            var stateCursor = await tx.RunAsync($$"""
                MERGE (a:User {id: $me})
                MERGE (b:User {id: $other})
                {{LockPair}}
                WITH a, b
                OPTIONAL MATCH (a)-[c:CONNECTED_WITH]-(b)
                WITH a, b, count(c) > 0 AS connected
                OPTIONAL MATCH (a)-[mine:REQUESTED]->(b)
                WITH a, b, connected, head(collect(CASE WHEN mine IS NULL THEN null ELSE coalesce(mine.status, 'PENDING') END)) AS myRequest
                OPTIONAL MATCH (b)-[theirs:REQUESTED]->(a)
                RETURN connected, myRequest,
                       any(status IN collect(CASE WHEN theirs IS NULL THEN null ELSE coalesce(theirs.status, 'PENDING') END) WHERE status = 'PENDING') AS theyRequested
                """, new { me = requesterUserId.ToString(), other = addresseeUserId.ToString() });
            var state = await stateCursor.SingleAsync();

            // Business rules are plain C# (unit tested); a ConflictException rolls the transaction back.
            ConnectionRules.EnsureCanRequest(new ConnectionState(
                state["connected"].As<bool>(),
                state["myRequest"].As<string?>(),
                state["theyRequested"].As<bool>()));

            await tx.RunAsync($$"""
                MATCH (a:User {id: $me}), (b:User {id: $other})
                MERGE (a)-[r:REQUESTED]->(b)
                SET r.status = 'PENDING', r.createdAtUtc = $now, r.respondedAtUtc = null
                WITH a
                {{CreateOutboxMessage}}
                """, OutboxParameters(@event, new { me = requesterUserId.ToString(), other = addresseeUserId.ToString() }));

            return new ConnectionDto(requesterUserId, addresseeUserId, RequestStatus.Pending, @event.OccurredAtUtc, null);
        });
    }

    public async Task<ConnectionDto?> AcceptAsync(Guid requesterUserId, Guid addresseeUserId, OutboxEnvelope @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = OpenSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            // Only a request pointing at the signed-in user (addressee) can match: nobody else can accept it.
            var cursor = await tx.RunAsync($$"""
                MATCH (a:User {id: $requester}), (b:User {id: $me})
                {{LockPair}}
                WITH a, b
                MATCH (a)-[request:REQUESTED]->(b)
                WHERE coalesce(request.status, 'PENDING') = 'PENDING'
                WITH a, b, request, request.createdAtUtc AS requestedAtUtc
                DELETE request
                WITH a, b, requestedAtUtc
                OPTIONAL MATCH (b)-[reverse:REQUESTED]->(a)
                DELETE reverse
                WITH DISTINCT a, b, requestedAtUtc
                MERGE (a)-[c:CONNECTED_WITH]-(b)
                  ON CREATE SET c.createdAtUtc = $now, c.requestedAtUtc = requestedAtUtc
                WITH requestedAtUtc, c
                {{CreateOutboxMessage}}
                RETURN requestedAtUtc, c.createdAtUtc AS connectedAtUtc
                """, OutboxParameters(@event, new { requester = requesterUserId.ToString(), me = addresseeUserId.ToString() }));

            var records = await cursor.ToListAsync();
            if (records.Count == 0) return null;
            var record = records[0];
            return new ConnectionDto(requesterUserId, addresseeUserId, "CONNECTED",
                ToUtc(record["requestedAtUtc"]), ToUtc(record["connectedAtUtc"]));
        });
    }

    public Task<bool> RejectAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken) =>
        WriteCountAsync("""
            MATCH (:User {id: $requester})-[request:REQUESTED]->(:User {id: $me})
            WHERE coalesce(request.status, 'PENDING') = 'PENDING'
            SET request.status = 'REJECTED', request.respondedAtUtc = $now
            RETURN count(request) AS changed
            """, new { requester = requesterUserId.ToString(), me = addresseeUserId.ToString(), now = DateTime.UtcNow }, cancellationToken);

    public Task<bool> WithdrawAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken) =>
        WriteCountAsync("""
            MATCH (:User {id: $me})-[request:REQUESTED]->(:User {id: $other})
            WHERE coalesce(request.status, 'PENDING') = 'PENDING'
            DELETE request
            RETURN count(*) AS changed
            """, new { me = requesterUserId.ToString(), other = addresseeUserId.ToString() }, cancellationToken);

    public Task<bool> RemoveAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken) =>
        WriteCountAsync("""
            MATCH (:User {id: $me})-[c:CONNECTED_WITH]-(:User {id: $other})
            DELETE c
            RETURN count(*) AS changed
            """, new { me = userId.ToString(), other = otherUserId.ToString() }, cancellationToken);

    public Task<IReadOnlyList<ConnectionEdge>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken) =>
        ReadEdgesAsync("""
            MATCH (:User {id: $id})-[c:CONNECTED_WITH]-(other:User)
            RETURN other.id AS userId, max(c.createdAtUtc) AS since
            ORDER BY since DESC
            """, userId, cancellationToken);

    public Task<IReadOnlyList<ConnectionEdge>> GetPendingReceivedAsync(Guid userId, CancellationToken cancellationToken) =>
        ReadEdgesAsync("""
            MATCH (other:User)-[r:REQUESTED]->(:User {id: $id})
            WHERE coalesce(r.status, 'PENDING') = 'PENDING'
            RETURN other.id AS userId, r.createdAtUtc AS since
            ORDER BY since DESC
            """, userId, cancellationToken);

    // Rejected requests are not listed: the requester is not told about a rejection, the request simply disappears.
    public Task<IReadOnlyList<ConnectionEdge>> GetPendingSentAsync(Guid userId, CancellationToken cancellationToken) =>
        ReadEdgesAsync("""
            MATCH (:User {id: $id})-[r:REQUESTED]->(other:User)
            WHERE coalesce(r.status, 'PENDING') = 'PENDING'
            RETURN other.id AS userId, r.createdAtUtc AS since
            ORDER BY since DESC
            """, userId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetMutualConnectionsAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = OpenSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("""
                MATCH (:User {id: $me})-[:CONNECTED_WITH]-(mutual:User)-[:CONNECTED_WITH]-(:User {id: $other})
                WHERE mutual.id <> $me AND mutual.id <> $other
                RETURN DISTINCT mutual.id AS userId
                """, new { me = userId.ToString(), other = otherUserId.ToString() });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<Guid>)records.Select(record => Guid.Parse(record["userId"].As<string>())).ToList();
        });
    }

    public async Task<ConnectionStatusDto> GetStatusAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = OpenSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("""
                OPTIONAL MATCH (me:User {id: $me})
                OPTIONAL MATCH (other:User {id: $other})
                OPTIONAL MATCH (me)-[c:CONNECTED_WITH]-(other)
                WITH me, other, count(c) > 0 AS connected
                OPTIONAL MATCH (me)-[sent:REQUESTED]->(other) WHERE coalesce(sent.status, 'PENDING') = 'PENDING'
                WITH me, other, connected, count(sent) > 0 AS sentPending
                OPTIONAL MATCH (other)-[received:REQUESTED]->(me) WHERE coalesce(received.status, 'PENDING') = 'PENDING'
                WITH me, other, connected, sentPending, count(received) > 0 AS receivedPending
                OPTIONAL MATCH (me)-[:CONNECTED_WITH]-(mutual:User)-[:CONNECTED_WITH]-(other)
                WHERE mutual <> me AND mutual <> other
                WITH other, connected, sentPending, receivedPending, count(DISTINCT mutual) AS mutualCount
                OPTIONAL MATCH (other)-[:CONNECTED_WITH]-(contact:User)
                RETURN connected, sentPending, receivedPending, mutualCount, count(DISTINCT contact) AS connectionCount
                """, new { me = userId.ToString(), other = otherUserId.ToString() });
            var record = await cursor.SingleAsync();

            var status = record["connected"].As<bool>() ? RelationshipStatus.Connected
                : record["sentPending"].As<bool>() ? RelationshipStatus.PendingSent
                : record["receivedPending"].As<bool>() ? RelationshipStatus.PendingReceived
                : RelationshipStatus.None;
            return new ConnectionStatusDto(status, record["mutualCount"].As<int>(), record["connectionCount"].As<int>());
        });
    }

    // ----- Network intelligence: questions that follow relationships, where a graph database shines -----

    public async Task<IReadOnlyList<SuggestionEdge>> GetSuggestionsAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = OpenSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            // Friends of my friends, excluding me, my connections and anyone I have a request with (in either
            // direction, including rejected ones, so a declined person is not suggested again).
            var cursor = await tx.RunAsync("""
                MATCH (me:User {id: $me})-[:CONNECTED_WITH]-(friend:User)-[:CONNECTED_WITH]-(candidate:User)
                WHERE candidate <> me
                  AND NOT (me)-[:CONNECTED_WITH]-(candidate)
                  AND NOT (me)-[:REQUESTED]-(candidate)
                RETURN candidate.id AS userId, count(DISTINCT friend) AS mutual
                ORDER BY mutual DESC, userId
                LIMIT $limit
                """, new { me = userId.ToString(), limit });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<SuggestionEdge>)records
                .Select(record => new SuggestionEdge(Guid.Parse(record["userId"].As<string>()), record["mutual"].As<int>()))
                .ToList();
        });
    }

    public async Task<IReadOnlyList<NetworkDegreeDto>> GetDegreesAsync(Guid userId, IReadOnlyCollection<Guid> candidateIds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (candidateIds.Count == 0) return [];
        await using var session = OpenSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("""
                MATCH (me:User {id: $me})
                UNWIND $candidates AS candidateId
                MATCH (candidate:User {id: candidateId})
                WHERE candidate <> me
                OPTIONAL MATCH (me)-[direct:CONNECTED_WITH]-(candidate)
                OPTIONAL MATCH (me)-[:CONNECTED_WITH]-(mutual:User)-[:CONNECTED_WITH]-(candidate)
                WHERE mutual <> me AND mutual <> candidate
                WITH candidateId, count(DISTINCT direct) > 0 AS connected, count(DISTINCT mutual) AS mutualCount
                WHERE connected OR mutualCount > 0
                RETURN candidateId AS userId, CASE WHEN connected THEN 1 ELSE 2 END AS degree, mutualCount
                """, new { me = userId.ToString(), candidates = candidateIds.Select(id => id.ToString()).Distinct().ToList() });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<NetworkDegreeDto>)records
                .Select(record => new NetworkDegreeDto(Guid.Parse(record["userId"].As<string>()), record["degree"].As<int>(), record["mutualCount"].As<int>()))
                .ToList();
        });
    }

    private async Task<bool> WriteCountAsync(string query, object parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = OpenSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(query, parameters);
            var record = await cursor.SingleAsync();
            return record["changed"].As<long>() > 0;
        });
    }

    private async Task<IReadOnlyList<ConnectionEdge>> ReadEdgesAsync(string query, Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = OpenSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(query, new { id = userId.ToString() });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<ConnectionEdge>)records
                .Select(record => new ConnectionEdge(Guid.Parse(record["userId"].As<string>()), ToUtc(record["since"])))
                .ToList();
        });
    }

    private static Dictionary<string, object?> OutboxParameters(OutboxEnvelope @event, object extra)
    {
        var parameters = extra.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(extra));
        parameters["outboxId"] = @event.Id.ToString();
        parameters["topic"] = @event.Topic;
        parameters["key"] = @event.Key;
        parameters["payload"] = @event.Payload;
        parameters["now"] = @event.OccurredAtUtc;
        parameters["traceParent"] = @event.TraceParent;
        return parameters;
    }

    private IAsyncSession OpenSession() => driver.AsyncSession(builder =>
    {
        if (!string.IsNullOrWhiteSpace(options.Value.Database)) builder.WithDatabase(options.Value.Database);
    });

    /// <summary>Neo4j returns UTC DateTime values as ZonedDateTime; older data may hold LocalDateTime.</summary>
    internal static DateTime ToUtc(object? value) => value switch
    {
        ZonedDateTime zoned => zoned.ToDateTimeOffset().UtcDateTime,
        LocalDateTime local => DateTime.SpecifyKind(local.ToDateTime(), DateTimeKind.Utc),
        DateTime dateTime => dateTime.ToUniversalTime(),
        DateTimeOffset offset => offset.UtcDateTime,
        _ => DateTime.MinValue
    };
}
