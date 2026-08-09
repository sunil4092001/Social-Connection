using ConnectionService.Application;
using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ConnectionService.Infrastructure.Persistence;

public sealed class Neo4jConnectionRepository(IDriver driver, IOptions<Neo4jOptions> options) : IConnectionRepository
{
    private void WithDatabase(SessionConfigBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.Database))
        {
            builder.WithDatabase(options.Value.Database);
        }
    }

    public async Task<ConnectionDto> RequestAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken)
    {
        await using var session = driver.AsyncSession(WithDatabase);
        var createdAtUtc = DateTime.UtcNow;

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("""
                MERGE (requester:User {id: $requesterUserId})
                MERGE (addressee:User {id: $addresseeUserId})
                MERGE (requester)-[connection:REQUESTED]->(addressee)
                ON CREATE SET connection.createdAtUtc = $createdAtUtc
                """, new
            {
                requesterUserId = requesterUserId.ToString(),
                addresseeUserId = addresseeUserId.ToString(),
                createdAtUtc
            });
        });

        return new ConnectionDto(requesterUserId, addresseeUserId, "Requested", createdAtUtc, null);
    }

    public async Task<ConnectionDto> AcceptAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken)
    {
        await using var session = driver.AsyncSession(WithDatabase);

        var connectedAtUtc = DateTime.UtcNow;
        var connection = await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync("""
                MATCH (requester:User {id: $requesterUserId})-[request:REQUESTED]->(addressee:User {id: $addresseeUserId})
                
                WITH request.createdAtUtc AS requestedAtUtc,
                     requester,
                     addressee,
                     request
                
                DELETE request
                MERGE (requester)-[connection:CONNECTED_WITH]->(addressee)
                ON CREATE SET 
                    connection.createdAtUtc = $connectedAtUtc
                    connection.requestedAtUtc = requestedAtUtc
                MERGE (addressee)-[reverseConnection:CONNECTED_WITH]->(requester)
                ON CREATE SET 
                    reverseConnection.requestedAtUtc = requestedAtUtc,
                    reverseConnection.createdAtUtc = $connectedAtUtc

                RETURN
                    requestedAtUtc,
                    connection.createdAtUtc AS connectedAtUtc
                """, new
            {
            //await tx.RunAsync("""
            //    MATCH (requester:User {id: $requesterUserId})-[request:REQUESTED]->(addressee:User {id: $addresseeUserId})
            //    DELETE request
            //    MERGE (requester)-[connection:CONNECTED_WITH]->(addressee)
            //    ON CREATE SET connection.createdAtUtc = $connectedAtUtc
            //    MERGE (addressee)-[reverseConnection:CONNECTED_WITH]->(requester)
            //    ON CREATE SET reverseConnection.createdAtUtc = $connectedAtUtc
            //    """, new
            //{
                requesterUserId = requesterUserId.ToString(),
                addresseeUserId = addresseeUserId.ToString(),
                connectedAtUtc
            });

            var record = await cursor.SingleAsync();

            var requestedAtUtc = record["requestedAtUtc"].As<ZonedDateTime>().ToDateTimeOffset().UtcDateTime;

            return new ConnectionDto(
               requesterUserId,
               addresseeUserId,
               "Connected",
               requestedAtUtc,//DateTime.MinValue,     // see note below
               connectedAtUtc);
        });
        return connection;
    }

    public async Task<IReadOnlyList<ConnectionDto>> GetAcceptedAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var session = driver.AsyncSession(WithDatabase);
        var cursor = await session.RunAsync("""
            MATCH (:User {id: $userId})-[connection:CONNECTED_WITH]->(connected:User)
            RETURN 
                connected.id AS connectedUserId, 
            connection.requestedAtUtc AS requestedAtUtc,
                connection.createdAtUtc AS createdAtUtc
            ORDER BY connected.id

            """, new { userId = userId.ToString() });

        var records = await cursor.ToListAsync();
        return records.Select(record => new ConnectionDto(
            userId,
            Guid.Parse(record["connectedUserId"].As<string>()),
            "Connected",
            record["requestedAtUtc"].As<ZonedDateTime>().ToDateTimeOffset().UtcDateTime,
            record["createdAtUtc"].As<ZonedDateTime>().ToDateTimeOffset().UtcDateTime)).ToList();
    }

    public async Task<IReadOnlyList<SentRequestDto>> GetAllSentConnectionRequestAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var session = driver.AsyncSession(WithDatabase);
        var cursor = await session.RunAsync("""
            MATCH (:User {id: $userId})-[connection:REQUESTED]->(connected:User)
            RETURN 
                connected.id AS connectedUserId, 
                connection.createdAtUtc AS createdAtUtc
            ORDER BY connected.id

            """, new { userId = userId.ToString() });

        var records = await cursor.ToListAsync();
        return records.Select(record => new SentRequestDto(
            userId,
            "First Name",
            "Last Name",
            record["createdAtUtc"].As<ZonedDateTime>().ToDateTimeOffset().UtcDateTime)).ToList();
    }
}
