using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Messaging.Outbox;

public static class OutboxExtensions
{
    /// <summary>Maps the OutboxMessages table. Call from OnModelCreating of a PostgreSQL DbContext.</summary>
    public static ModelBuilder ConfigureOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Topic).HasMaxLength(200).IsRequired();
            e.Property(x => x.MessageKey).HasMaxLength(200).IsRequired();
            e.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.Property(x => x.TraceParent).HasMaxLength(100);
            // The relay only ever reads unprocessed rows, oldest first.
            e.HasIndex(x => x.OccurredAtUtc).HasFilter("\"ProcessedAtUtc\" IS NULL");
        });
        return modelBuilder;
    }

    /// <summary>
    /// Transactional outbox for an EF Core service: IEventOutbox stages events on <typeparamref name="TContext"/>,
    /// a background relay publishes them to Kafka.
    /// </summary>
    public static IServiceCollection AddEfOutbox<TContext>(this IServiceCollection services) where TContext : DbContext
    {
        services.AddScoped<IEventOutbox, EfEventOutbox<TContext>>();
        services.TryAddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<OutboxPublisherService<TContext>>();
        return services;
    }
}
