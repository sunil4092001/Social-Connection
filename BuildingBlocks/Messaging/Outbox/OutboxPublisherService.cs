using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Messaging.Outbox;

/// <summary>
/// Background relay: publishes pending outbox rows to Kafka in the order they were created and marks them
/// as processed. When publishing fails (typically Kafka is down) the batch stops, so later events never overtake
/// an earlier one, and the relay backs off exponentially (2s up to 60s). Rows are never skipped;
/// Attempts and LastError show what is stuck. Delivery is at-least-once; consumers deduplicate by EventId.
/// </summary>
public sealed class OutboxPublisherService<TContext>(
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    ILogger<OutboxPublisherService<TContext>> logger) : BackgroundService where TContext : DbContext
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!publisher.IsEnabled)
        {
            logger.LogWarning("Outbox relay is not running because Kafka is not configured; events stay pending in OutboxMessages.");
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
                // Database unavailable or similar: try again later.
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

    /// <summary>Publishes one batch. Returns false when a message could not be published.</summary>
    public async Task<bool> PublishPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = await db.Set<OutboxMessage>()
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var succeeded = true;
        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message.Topic, message.MessageKey, message.Payload, cancellationToken, message.TraceParent);
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
                logger.LogWarning("Publishing outbox message {MessageId} to {Topic} failed (attempt {Attempt}): {Error}",
                    message.Id, message.Topic, message.Attempts, message.LastError);
                // Stop this batch so later events are not published before an earlier one (keeps ordering).
                succeeded = false;
                break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return succeeded;
    }
}
