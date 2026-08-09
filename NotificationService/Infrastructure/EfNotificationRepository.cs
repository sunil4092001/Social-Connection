using Microsoft.EntityFrameworkCore;
using NotificationService.Application;
using NotificationService.Domain;

namespace NotificationService.Infrastructure;

public sealed class EfNotificationRepository(NotificationDbContext dbContext) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        await dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.FindAsync([id], cancellationToken);
        if (notification is null)
        {
            return;
        }

        notification.IsRead = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
