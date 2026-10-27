using BuildingBlocks.Common;
using BuildingBlocks.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application;

namespace NotificationService.Controllers;

/// <summary>
/// Read side of notifications for the signed-in user. Notifications are created from Kafka events only,
/// so there is no create endpoint. The recipient is always the user id from the JWT.
/// </summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<NotificationPageDto>>> GetForCurrentUser([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var recipientUserId = User.GetUserId();
        if (recipientUserId == Guid.Empty) return Unauthorized();
        var result = await mediator.Send(new GetNotificationsQuery(recipientUserId, page, pageSize), cancellationToken);
        return Ok(ApiResponseFactory.Success(result, "Notifications."));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var recipientUserId = User.GetUserId();
        if (recipientUserId == Guid.Empty) return Unauthorized();
        var count = await mediator.Send(new GetUnreadCountQuery(recipientUserId), cancellationToken);
        return Ok(ApiResponseFactory.Success(count, "Unread notifications."));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var recipientUserId = User.GetUserId();
        if (recipientUserId == Guid.Empty) return Unauthorized();
        await mediator.Send(new MarkNotificationReadCommand(id, recipientUserId), cancellationToken);
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<ApiResponse<int>>> MarkAllRead(CancellationToken cancellationToken)
    {
        var recipientUserId = User.GetUserId();
        if (recipientUserId == Guid.Empty) return Unauthorized();
        var updated = await mediator.Send(new MarkAllNotificationsReadCommand(recipientUserId), cancellationToken);
        return Ok(ApiResponseFactory.Success(updated, "All notifications marked as read."));
    }
}
