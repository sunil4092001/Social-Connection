namespace BuildingBlocks.Messaging.Outbox;

/// <summary>
/// An integration event waiting to be published. It is inserted in the same database transaction as the
/// business change, so a saved change always has its event, even if Kafka is down at that moment.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string MessageKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    /// <summary>W3C trace id of the request that produced the event, so publishing and consuming join that trace.</summary>
    public string? TraceParent { get; set; }
}
