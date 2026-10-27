using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ConnectionService.Infrastructure.Persistence;

/// <summary>
/// Runs once at startup. Creates constraints/indexes and upgrades data written by the earlier model.
/// Every statement is idempotent, so restarting the service is safe.
/// </summary>
public sealed class Neo4jSchemaInitializer(IDriver driver, IOptions<Neo4jOptions> options, ILogger<Neo4jSchemaInitializer> logger) : IHostedService
{
    private static readonly string[] Statements =
    [
        // One node per user id; also gives the id lookup an index.
        "CREATE CONSTRAINT user_id_unique IF NOT EXISTS FOR (u:User) REQUIRE u.id IS UNIQUE",
        "CREATE CONSTRAINT outbox_id_unique IF NOT EXISTS FOR (m:OutboxMessage) REQUIRE m.id IS UNIQUE",
        "CREATE INDEX outbox_pending IF NOT EXISTS FOR (m:OutboxMessage) ON (m.processedAtUtc)",

        // Earlier versions stored every connection twice (A->B and B->A). Keep one; queries ignore direction.
        """
        MATCH (a:User)-[first:CONNECTED_WITH]->(b:User)-[second:CONNECTED_WITH]->(a)
        WHERE elementId(first) < elementId(second)
        DELETE second
        """,
        // Earlier requests have no status; they are pending.
        "MATCH ()-[r:REQUESTED]->() WHERE r.status IS NULL SET r.status = 'PENDING'"
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var session = driver.AsyncSession(builder =>
            {
                if (!string.IsNullOrWhiteSpace(options.Value.Database)) builder.WithDatabase(options.Value.Database);
            });
            foreach (var statement in Statements)
            {
                var cursor = await session.RunAsync(statement);
                await cursor.ConsumeAsync();
            }
            logger.LogInformation("Neo4j schema is ready (database {Database})", options.Value.Database);
        }
        catch (Exception exception)
        {
            // Do not stop the service: the API reports errors per request until Neo4j is reachable.
            logger.LogError(exception, "Neo4j schema initialisation failed; is Neo4j running and is database {Database} available?", options.Value.Database);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
