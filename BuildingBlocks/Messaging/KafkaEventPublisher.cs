using System.Text;
using BuildingBlocks.Observability;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Kafka producer shared by every service that publishes events.
/// Messages are keyed (for example by requirement id) so Kafka keeps events about the same entity in order.
/// </summary>
public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string>? producer;
    private readonly ILogger<KafkaEventPublisher> logger;

    public KafkaEventPublisher(IConfiguration configuration, ILogger<KafkaEventPublisher> logger)
    {
        this.logger = logger;
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            logger.LogWarning("Kafka publishing is disabled because Kafka:BootstrapServers is not configured.");
            return;
        }

        producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = Math.Clamp(configuration.GetValue("Kafka:MessageTimeoutMs", 8000), 1000, 60000)
        }).Build();
    }

    public bool IsEnabled => producer is not null;

    public async Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken, string? traceParent = null)
    {
        if (producer is null) throw new InvalidOperationException("Kafka publishing is disabled.");

        using var activity = MessagingTracing.StartPublish(topic, traceParent);
        var message = new Message<string, string> { Key = key, Value = payload, Headers = [] };
        // Consumers continue the same trace from this header.
        var outgoingTrace = activity?.Id ?? traceParent;
        if (outgoingTrace is not null) message.Headers.Add(MessagingTracing.TraceParentHeader, Encoding.UTF8.GetBytes(outgoingTrace));

        var result = await producer.ProduceAsync(topic, message, cancellationToken);
        logger.LogInformation("Published event to {Topic} (key {Key}) at partition {Partition} offset {Offset}",
            topic, key, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose()
    {
        producer?.Flush(TimeSpan.FromSeconds(5));
        producer?.Dispose();
    }
}

public static class KafkaEventPublisherExtensions
{
    public static IServiceCollection AddKafkaEventPublisher(this IServiceCollection services) =>
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
}
