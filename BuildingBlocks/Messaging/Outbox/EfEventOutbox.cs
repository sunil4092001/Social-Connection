using System.Text.Json;
using BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Messaging.Outbox;

/// <summary>Stages events on the service's DbContext; the next SaveChangesAsync commits change and events together.</summary>
public sealed class EfEventOutbox<TContext>(TContext db) : IEventOutbox where TContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add<TEvent>(string topic, string key, TEvent @event) where TEvent : notnull =>
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = topic,
            MessageKey = key,
            Payload = JsonSerializer.Serialize(@event, JsonOptions),
            OccurredAtUtc = DateTime.UtcNow,
            TraceParent = MessagingTracing.CurrentTraceParent()
        });
}
