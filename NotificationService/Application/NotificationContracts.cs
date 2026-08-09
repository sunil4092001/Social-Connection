using FluentValidation;
using MediatR;
using NotificationService.Domain;

namespace NotificationService.Application;

public sealed record CreateNotificationRequest(Guid RecipientUserId, string Type, string Message);
public sealed record NotificationDto(Guid Id, Guid RecipientUserId, string Type, string Message, bool IsRead, DateTime CreatedAtUtc);
public sealed record CreateNotificationCommand(Guid RecipientUserId, string Type, string Message) : IRequest<NotificationDto>;
public sealed record GetNotificationsQuery(Guid RecipientUserId) : IRequest<IReadOnlyList<NotificationDto>>;
public sealed record MarkNotificationReadCommand(Guid Id) : IRequest;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> GetForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class CreateNotificationValidator : AbstractValidator<CreateNotificationCommand>
{
    public CreateNotificationValidator()
    {
        RuleFor(command => command.RecipientUserId).NotEmpty();
        RuleFor(command => command.Type).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Message).NotEmpty().MaximumLength(500);
    }
}

public sealed class CreateNotificationCommandHandler(INotificationRepository repository)
    : IRequestHandler<CreateNotificationCommand, NotificationDto>
{
    public async Task<NotificationDto> Handle(CreateNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = request.RecipientUserId,
            Type = request.Type.Trim(),
            Message = request.Message.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(notification, cancellationToken);
        return notification.ToDto();
    }
}

public sealed class GetNotificationsQueryHandler(INotificationRepository repository)
    : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public async Task<IReadOnlyList<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifications = await repository.GetForRecipientAsync(request.RecipientUserId, cancellationToken);
        return notifications.Select(notification => notification.ToDto()).ToList();
    }
}

public sealed class MarkNotificationReadCommandHandler(INotificationRepository repository)
    : IRequestHandler<MarkNotificationReadCommand>
{
    public Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken) =>
        repository.MarkReadAsync(request.Id, cancellationToken);
}

public static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification notification) =>
        new(notification.Id, notification.RecipientUserId, notification.Type, notification.Message, notification.IsRead, notification.CreatedAtUtc);
}
