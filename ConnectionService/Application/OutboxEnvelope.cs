using System.Text.Json;
using BuildingBlocks.Observability;

namespace ConnectionService.Application;

/// <summary>An integration event ready to be stored as an outbox node in the same transaction as the graph change.</summary>
public sealed record OutboxEnvelope(Guid Id, string Topic, string Key, string Payload, DateTime OccurredAtUtc, string? TraceParent = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Events about the same pair of users share one Kafka key (smaller id first), so "requested" is always
    /// delivered before "accepted".
    /// </summary>
    public static OutboxEnvelope For<TEvent>(string topic, Guid userA, Guid userB, TEvent @event, DateTime occurredAtUtc) where TEvent : notnull
    {
        var key = userA.CompareTo(userB) <= 0 ? $"{userA}:{userB}" : $"{userB}:{userA}";
        return new OutboxEnvelope(Guid.NewGuid(), topic, key, JsonSerializer.Serialize(@event, JsonOptions), occurredAtUtc, MessagingTracing.CurrentTraceParent());
    }
}
