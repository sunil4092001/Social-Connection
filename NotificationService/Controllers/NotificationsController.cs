using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NotificationDto>> Create(CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        var notification = await mediator.Send(
            new CreateNotificationCommand(request.RecipientUserId, request.Type, request.Message),
            cancellationToken);

        return CreatedAtAction(nameof(GetForRecipient), new { recipientUserId = notification.RecipientUserId }, notification);
    }

    [HttpGet("{recipientUserId:guid}")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetForRecipient(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var notifications = await mediator.Send(new GetNotificationsQuery(recipientUserId), cancellationToken);
        return Ok(notifications);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkNotificationReadCommand(id), cancellationToken);
        return NoContent();
    }
}
