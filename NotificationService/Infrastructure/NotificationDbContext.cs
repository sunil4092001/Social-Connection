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
            entity.Property(notification => notification.Type).HasMaxLength(80).IsRequired();
            entity.Property(notification => notification.Message).HasMaxLength(500).IsRequired();
            entity.Property(notification => notification.CreatedAtUtc).IsRequired();
            entity.HasIndex(notification => notification.RecipientUserId);
        });
    }
}
