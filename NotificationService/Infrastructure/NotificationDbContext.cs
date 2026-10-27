using Microsoft.EntityFrameworkCore;
using NotificationService.Domain;

namespace NotificationService.Infrastructure;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.RecipientUserId).IsRequired();
            entity.Property(notification => notification.EventId);
            entity.Property(notification => notification.Type).HasMaxLength(80).IsRequired();
            entity.Property(notification => notification.Title).HasMaxLength(160).IsRequired();
            entity.Property(notification => notification.Message).HasMaxLength(500).IsRequired();
            entity.Property(notification => notification.ReferenceType).HasMaxLength(40);
            entity.Property(notification => notification.CreatedAtUtc).IsRequired();
            // Supports "my notifications, newest first" (paging) and "my unread count" (header badge).
            entity.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAtUtc });
            entity.HasIndex(notification => new { notification.RecipientUserId, notification.IsRead });
            entity.HasIndex(notification => new { notification.EventId, notification.RecipientUserId })
                .IsUnique()
                .HasFilter("[EventId] IS NOT NULL");
        });
    }
}
