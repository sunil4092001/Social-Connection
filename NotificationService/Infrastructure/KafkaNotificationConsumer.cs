using System.Text;
using System.Text.Json;
using BuildingBlocks.Events;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using Confluent.Kafka;
using FluentValidation;
using MediatR;
using NotificationService.Application;

namespace NotificationService.Infrastructure;

/// <summary>
/// Consumes integration events and stores in-app notifications.
///
/// Failure handling:
///  * Permanent failure (unreadable JSON, invalid data): the message can never succeed, so it is copied to the
///    dead-letter topic and committed; the partition keeps moving.
///  * Transient failure (database down, etc.): the offset is not committed, the consumer seeks back and retries
///    with exponential backoff (2s up to 60s). Nothing is lost and order is preserved.
/// Duplicates are harmless because notifications are unique per (EventId, recipient).
/// </summary>
public sealed class KafkaNotificationConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IEventPublisher deadLetterPublisher,
    ILogger<KafkaNotificationConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        var groupId = configuration["Kafka:GroupId"];
        if (string.IsNullOrWhiteSpace(bootstrapServers) || string.IsNullOrWhiteSpace(groupId))
        {
            logger.LogInformation("Kafka notification consumer is disabled because Kafka configuration is missing.");
            return Task.CompletedTask;
        }

        // Consume() blocks, so run the loop on its own thread instead of the host's startup thread.
        return Task.Run(() => ConsumeLoopAsync(bootstrapServers, groupId, stoppingToken), stoppingToken);
    }

    private async Task ConsumeLoopAsync(string bootstrapServers, string groupId, CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string?, string>(config).Build();
        consumer.Subscribe(NotificationEventMapper.Topics);
        var backoff = InitialBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string?, string> result;
            try
            {
                result = consumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException exception)
            {
                // For example a topic that does not exist yet. Not fatal; keep polling.
                logger.LogWarning("Kafka consume error: {Reason}", exception.Error.Reason);
                await DelayAsync(InitialBackoff, stoppingToken);
                continue;
            }

            // Continue the producer's trace so one trace shows request -> outbox -> Kafka -> notification.
            using var activity = MessagingTracing.StartConsume(result.Topic, TraceParentOf(result.Message));
            try
            {
                await ProcessAsync(result.Topic, result.Message.Value, stoppingToken);
                consumer.Commit(result);
                backoff = InitialBackoff;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (IsPermanent(exception))
            {
                logger.LogError(exception, "Event from {Topic} at offset {Offset} can never be processed; sending it to the dead-letter topic", result.Topic, result.Offset);
                if (await TryDeadLetterAsync(result, exception, stoppingToken))
                {
                    consumer.Commit(result);
                }
                else
                {
                    backoff = await RetryLaterAsync(consumer, result, backoff, stoppingToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Processing event from {Topic} at offset {Offset} failed; retrying in {Delay}", result.Topic, result.Offset, backoff);
                backoff = await RetryLaterAsync(consumer, result, backoff, stoppingToken);
            }
        }

        consumer.Close();
    }

    private async Task ProcessAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        var commands = NotificationEventMapper.Map(topic, payload);
        if (commands.Count == 0)
        {
            logger.LogDebug("No notifications for event from {Topic}", topic);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        foreach (var command in commands)
        {
            await mediator.Send(command, cancellationToken);
        }
    }

    private static string? TraceParentOf(Message<string?, string> message) =>
        message.Headers?.TryGetLastBytes(MessagingTracing.TraceParentHeader, out var bytes) == true ? Encoding.UTF8.GetString(bytes) : null;

    /// <summary>Failures that will happen again no matter how often the message is retried.</summary>
    public static bool IsPermanent(Exception exception) =>
        exception is JsonException or ValidationException or NotSupportedException or ArgumentException;

    private async Task<bool> TryDeadLetterAsync(ConsumeResult<string?, string> result, Exception exception, CancellationToken cancellationToken)
    {
        if (!deadLetterPublisher.IsEnabled) return false;
        try
        {
            var envelope = new DeadLetterMessage(result.Topic, result.Partition.Value, result.Offset.Value, result.Message.Key,
                result.Message.Value, $"{exception.GetType().Name}: {exception.Message}", DateTime.UtcNow);
            await deadLetterPublisher.PublishAsync(EventTopics.NotificationDeadLetter, result.Topic, JsonSerializer.Serialize(envelope, JsonOptions), cancellationToken);
            return true;
        }
        catch (Exception publishException) when (publishException is not OperationCanceledException)
        {
            // Never commit a message we could neither process nor park; try again later.
            logger.LogError(publishException, "Could not publish to the dead-letter topic; the event will be retried");
            return false;
        }
    }

    private static async Task<TimeSpan> RetryLaterAsync(IConsumer<string?, string> consumer, ConsumeResult<string?, string> result, TimeSpan backoff, CancellationToken cancellationToken)
    {
        // Rewind so the next Consume() returns the same message.
        consumer.Seek(result.TopicPartitionOffset);
        await DelayAsync(backoff, cancellationToken);
        return TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}

/// <summary>What is written to the dead-letter topic: the original message plus why it failed, for inspection or replay.</summary>
public sealed record DeadLetterMessage(string OriginalTopic, int Partition, long Offset, string? Key, string Payload, string Error, DateTime FailedAtUtc);
