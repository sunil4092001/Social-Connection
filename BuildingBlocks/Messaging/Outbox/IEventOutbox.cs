namespace BuildingBlocks.Messaging.Outbox;

/// <summary>
/// Stages an integration event to be saved together with the current change and published afterwards.
/// Nothing is written until the handler saves its unit of work.
/// </summary>
public interface IEventOutbox
{
    /// <param name="key">Kafka message key; events with the same key are delivered in order.</param>
    void Add<TEvent>(string topic, string key, TEvent @event) where TEvent : notnull;
}
