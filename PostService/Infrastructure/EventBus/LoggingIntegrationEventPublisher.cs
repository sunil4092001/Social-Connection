using PostService.Application;
using PostService.Application.Interfaces;

namespace PostService.Infrastructure.EventBus;

public sealed class LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger)
    : IIntegrationEventPublisher
{
    public Task PublishAsync(string topic, object payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Publishing Kafka integration event {Topic}: {@Payload}", topic, payload);
        return Task.CompletedTask;
    }
}
