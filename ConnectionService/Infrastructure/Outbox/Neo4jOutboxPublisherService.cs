using BuildingBlocks.Messaging;
using ConnectionService.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ConnectionService.Infrastructure.Outbox;

/// <summary>
/// Publishes pending (:OutboxMessage) nodes to Kafka, oldest first, and marks them processed.
/// Same behaviour as RequirementService's EF outbox relay: a failure stops the batch (keeps order) and the relay
/// backs off exponentially (2s up to 60s). Processed messages are deleted after seven days.
/// </summary>
public sealed class Neo4jOutboxPublisherService(
    IDriver driver,
    IOptions<Neo4jOptions> options,
    IEventPublisher publisher,
    ILogger<Neo4jOutboxPublisherService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!publisher.IsEnabled)
        {
            logger.LogWarning("Outbox relay is not running because Kafka is not configured; events stay pending in Neo4j.");
            return;
        }

        var delay = PollInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            bool succeeded;
            try
            {
                succeeded = await PublishPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Outbox relay iteration failed");
                succeeded = false;
            }

            delay = succeeded ? PollInterval : TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxBackoff.Ticks));
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var session = driver.AsyncSession(builder =>
        {
            if (!string.IsNullOrWhiteSpace(options.Value.Database)) builder.WithDatabase(options.Value.Database);
        });

        var pending = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("""
                MATCH (m:OutboxMessage) WHERE m.processedAtUtc IS NULL
                RETURN m.id AS id, m.topic AS topic, m.key AS key, m.payload AS payload, m.traceParent AS traceParent
                ORDER BY m.occurredAtUtc
                LIMIT $limit
                """, new { limit = BatchSize });
            return await cursor.ToListAsync();
        });

        foreach (var message in pending)
        {
            var id = message["id"].As<string>();
            try
            {
                await publisher.PublishAsync(message["topic"].As<string>(), message["key"].As<string>(), message["payload"].As<string>(), cancellationToken, message["traceParent"].As<string?>());
                await session.ExecuteWriteAsync(tx => tx.RunAsync(
                    "MATCH (m:OutboxMessage {id: $id}) SET m.processedAtUtc = $now, m.lastError = null",
                    new { id, now = DateTime.UtcNow }));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var error = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
                await session.ExecuteWriteAsync(tx => tx.RunAsync(
                    "MATCH (m:OutboxMessage {id: $id}) SET m.attempts = coalesce(m.attempts, 0) + 1, m.lastError = $error",
                    new { id, error }));
                logger.LogWarning("Publishing outbox message {MessageId} failed: {Error}", id, error);
                return false;
            }
        }

        if (pending.Count > 0)
        {
            await session.ExecuteWriteAsync(tx => tx.RunAsync(
                "MATCH (m:OutboxMessage) WHERE m.processedAtUtc < $cutoff WITH m LIMIT 1000 DELETE m",
                new { cutoff = DateTime.UtcNow - Retention }));
        }
        return true;
    }
}
