namespace BuildingBlocks.Messaging;

/// <summary>
/// Sends an already-serialized integration event to the message broker.
/// Implementations throw when delivery fails so callers (the outbox relays) can retry.
/// </summary>
public interface IEventPublisher
{
    /// <summary>False when no broker is configured; relays then leave events pending instead of failing.</summary>
    bool IsEnabled { get; }

    /// <param name="traceParent">Trace of the request that produced the event (from the outbox); null uses the current trace.</param>
    Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken, string? traceParent = null);
}
