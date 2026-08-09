namespace PostService.Application.Interfaces
{
    public interface IIntegrationEventPublisher
    {
        Task PublishAsync(string topic, object payload, CancellationToken cancellationToken);

    }
}
