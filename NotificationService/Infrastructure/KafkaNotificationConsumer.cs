using Confluent.Kafka;

namespace NotificationService.Infrastructure;

public sealed class KafkaNotificationConsumer(
    IConfiguration configuration,
    ILogger<KafkaNotificationConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        var groupId = configuration["Kafka:GroupId"];

        if (string.IsNullOrWhiteSpace(bootstrapServers) || string.IsNullOrWhiteSpace(groupId))
        {
            logger.LogInformation("Kafka notification consumer is disabled because Kafka configuration is missing.");
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe("posts.created");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    logger.LogInformation("Consumed Kafka notification event from {Topic}: {Message}", result.Topic, result.Message.Value);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, stoppingToken);
    }
}
