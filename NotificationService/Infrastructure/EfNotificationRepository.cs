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

    public Task<Notification?> GetByEventAsync(Guid eventId, Guid recipientUserId, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(notification => notification.EventId == eventId && notification.RecipientUserId == recipientUserId, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetPageForRecipientAsync(Guid recipientUserId, int page, int pageSize, CancellationToken cancellationToken) =>
        await dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public Task<int> CountForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        dbContext.Notifications.CountAsync(notification => notification.RecipientUserId == recipientUserId, cancellationToken);

    public Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        dbContext.Notifications.CountAsync(notification => notification.RecipientUserId == recipientUserId && !notification.IsRead, cancellationToken);

    public async Task<bool> MarkReadAsync(Guid id, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.FirstOrDefaultAsync(
            item => item.Id == id && item.RecipientUserId == recipientUserId,
            cancellationToken);
        if (notification is null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public Task<int> MarkAllReadAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        // Single UPDATE statement; no need to load the rows.
        dbContext.Notifications
            .Where(notification => notification.RecipientUserId == recipientUserId && !notification.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.IsRead, true), cancellationToken);
}
