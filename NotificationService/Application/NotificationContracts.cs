using FluentValidation;
using MediatR;
using NotificationService.Domain;

namespace NotificationService.Application;

public sealed record NotificationDto(Guid Id, Guid RecipientUserId, string Type, string Title, string Message, Guid? ReferenceId, string? ReferenceType, bool IsRead, DateTime CreatedAtUtc);
public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Page, int PageSize, int TotalCount, int UnreadCount);

/// <summary>Created only by the Kafka consumer from events of other services; there is no public create endpoint.</summary>
public sealed record CreateNotificationCommand(Guid RecipientUserId, string Type, string Title, string Message, Guid? ReferenceId, string? ReferenceType, Guid? EventId = null) : IRequest<NotificationDto>;
public sealed record GetNotificationsQuery(Guid RecipientUserId, int Page = 1, int PageSize = 20) : IRequest<NotificationPageDto>;
public sealed record GetUnreadCountQuery(Guid RecipientUserId) : IRequest<int>;
public sealed record MarkNotificationReadCommand(Guid Id, Guid RecipientUserId) : IRequest;
public sealed record MarkAllNotificationsReadCommand(Guid RecipientUserId) : IRequest<int>;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken);
    Task<Notification?> GetByEventAsync(Guid eventId, Guid recipientUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> GetPageForRecipientAsync(Guid recipientUserId, int page, int pageSize, CancellationToken cancellationToken);
    Task<int> CountForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);
    Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken);
    /// <summary>Returns false when the notification does not exist or belongs to someone else.</summary>
    Task<bool> MarkReadAsync(Guid id, Guid recipientUserId, CancellationToken cancellationToken);
    Task<int> MarkAllReadAsync(Guid recipientUserId, CancellationToken cancellationToken);
}

// ----- Validators -----

public sealed class CreateNotificationValidator : AbstractValidator<CreateNotificationCommand>
{
    public CreateNotificationValidator()
    {
        RuleFor(command => command.RecipientUserId).NotEmpty();
        RuleFor(command => command.Type).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Title).NotEmpty().MaximumLength(160);
        RuleFor(command => command.Message).NotEmpty().MaximumLength(500);
        RuleFor(command => command.ReferenceType).MaximumLength(40);
    }
}

public sealed class GetNotificationsValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsValidator()
    {
        RuleFor(query => query.RecipientUserId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class GetUnreadCountValidator : AbstractValidator<GetUnreadCountQuery>
{
    public GetUnreadCountValidator() => RuleFor(query => query.RecipientUserId).NotEmpty();
}

public sealed class MarkNotificationReadValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.RecipientUserId).NotEmpty();
    }
}

public sealed class MarkAllNotificationsReadValidator : AbstractValidator<MarkAllNotificationsReadCommand>
{
    public MarkAllNotificationsReadValidator() => RuleFor(command => command.RecipientUserId).NotEmpty();
}

// ----- Handlers -----

public sealed class CreateNotificationCommandHandler(INotificationRepository repository, ILogger<CreateNotificationCommandHandler> logger)
    : IRequestHandler<CreateNotificationCommand, NotificationDto>
{
    public async Task<NotificationDto> Handle(CreateNotificationCommand request, CancellationToken cancellationToken)
    {
        // Kafka delivers at least once: the same event may arrive again, so one notification per (event, recipient).
        if (request.EventId is Guid eventId)
        {
            var existing = await repository.GetByEventAsync(eventId, request.RecipientUserId, cancellationToken);
            if (existing is not null)
            {
                logger.LogInformation("Skipped duplicate notification for event {EventId} and recipient {RecipientUserId}", eventId, request.RecipientUserId);
                return existing.ToDto();
            }
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = request.RecipientUserId,
            EventId = request.EventId,
            Type = request.Type.Trim(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            ReferenceId = request.ReferenceId,
            ReferenceType = request.ReferenceType?.Trim(),
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(notification, cancellationToken);
        return notification.ToDto();
    }
}

public sealed class GetNotificationsQueryHandler(INotificationRepository repository)
    : IRequestHandler<GetNotificationsQuery, NotificationPageDto>
{
    public async Task<NotificationPageDto> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var items = await repository.GetPageForRecipientAsync(request.RecipientUserId, request.Page, request.PageSize, cancellationToken);
        var total = await repository.CountForRecipientAsync(request.RecipientUserId, cancellationToken);
        var unread = await repository.CountUnreadAsync(request.RecipientUserId, cancellationToken);
        return new NotificationPageDto(items.Select(notification => notification.ToDto()).ToList(), request.Page, request.PageSize, total, unread);
    }
}

public sealed class GetUnreadCountQueryHandler(INotificationRepository repository) : IRequestHandler<GetUnreadCountQuery, int>
{
    public Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken) =>
        repository.CountUnreadAsync(request.RecipientUserId, cancellationToken);
}

public sealed class MarkNotificationReadCommandHandler(INotificationRepository repository)
    : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        // Someone else's notification is reported as "not found" so its existence is not revealed.
        if (!await repository.MarkReadAsync(request.Id, request.RecipientUserId, cancellationToken))
            throw new KeyNotFoundException("Notification was not found.");
    }
}

public sealed class MarkAllNotificationsReadCommandHandler(INotificationRepository repository)
    : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    public Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken) =>
        repository.MarkAllReadAsync(request.RecipientUserId, cancellationToken);
}

public static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification notification) =>
        new(notification.Id, notification.RecipientUserId, notification.Type, notification.Title, notification.Message, notification.ReferenceId, notification.ReferenceType, notification.IsRead, notification.CreatedAtUtc);
}
