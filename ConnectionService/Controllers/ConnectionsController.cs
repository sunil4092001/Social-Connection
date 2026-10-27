using BuildingBlocks.Common;
using BuildingBlocks.Response;
using ConnectionService.Application.Commands;
using ConnectionService.Application.DTOs;
using ConnectionService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectionService.Controllers;

/// <summary>
/// Connections between platform users (customers and professionals alike). The signed-in user is always taken
/// from the JWT; only the addressee of a request can accept or reject it, only its sender can withdraw it.
/// A connection is independent of a professional's interest in a requirement.
/// </summary>
[ApiController]
[Authorize]
[Route("api/connections")]
public sealed class ConnectionsController(IMediator mediator) : ControllerBase
{
    [HttpPost("request")]
    public async Task<ActionResult<ApiResponse<ConnectionDto>>> CreateRequest([FromQuery] Guid addresseeUserId, CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        var connection = await mediator.Send(new RequestConnectionCommand(me, addresseeUserId), cancellationToken);
        return Ok(ApiResponseFactory.Success(connection, "Connection request sent."));
    }

    [HttpPost("accept")]
    public async Task<ActionResult<ApiResponse<ConnectionDto>>> Accept(RespondToConnectionRequest request, CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        var connection = await mediator.Send(new AcceptConnectionCommand(request.RequesterUserId, me), cancellationToken);
        return Ok(ApiResponseFactory.Success(connection, "Connection request accepted."));
    }

    [HttpPost("reject")]
    public async Task<IActionResult> Reject(RespondToConnectionRequest request, CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        await mediator.Send(new RejectConnectionCommand(request.RequesterUserId, me), cancellationToken);
        return NoContent();
    }

    /// <summary>Pending requests other users sent to me.</summary>
    [HttpGet("requests")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConnectionUserDto>>>> GetReceivedRequests(CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new GetReceivedRequestsQuery(me), cancellationToken), "Received connection requests."));
    }

    /// <summary>Pending requests I sent.</summary>
    [HttpGet("sent-requests")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConnectionUserDto>>>> GetSentRequests(CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new GetSentConnectionsQuery(me), cancellationToken), "Sent connection requests."));
    }

    [HttpDelete("sent-requests/{addresseeUserId:guid}")]
    public async Task<IActionResult> Withdraw(Guid addresseeUserId, CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        await mediator.Send(new WithdrawConnectionRequestCommand(me, addresseeUserId), cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConnectionUserDto>>>> GetMine(CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new GetConnectionsQuery(me), cancellationToken), "My connections."));
    }

    [HttpGet("status/{otherUserId:guid}")]
    public async Task<ActionResult<ApiResponse<ConnectionStatusDto>>> GetStatus(Guid otherUserId, CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new GetConnectionStatusQuery(me, otherUserId), cancellationToken), "Connection status."));
    }

    [HttpGet("mutual/{otherUserId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConnectionUserDto>>>> GetMutual(Guid otherUserId, CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new GetMutualConnectionsQuery(me, otherUserId), cancellationToken), "Mutual connections."));
    }

    /// <summary>People you may know: 2nd-degree contacts ranked by mutual connections.</summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConnectionSuggestionDto>>>> GetSuggestions([FromQuery] int limit = 10, CancellationToken cancellationToken = default)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new GetConnectionSuggestionsQuery(me, limit), cancellationToken), "Suggestions."));
    }

    /// <summary>
    /// For each given user: 1st or 2nd degree from the caller, with mutual connection count. Users outside the
    /// caller's network are omitted. Used by RequirementService (trust bonus) and the professional directory.
    /// </summary>
    [HttpPost("degrees")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NetworkDegreeDto>>>> GetDegrees(NetworkDegreesRequest request, CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new GetNetworkDegreesQuery(me, request.UserIds ?? []), cancellationToken), "Network degrees."));
    }

    /// <summary>Another user's connections (visible to any signed-in user, as on a professional network).</summary>
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConnectionUserDto>>>> GetForUser(Guid userId, CancellationToken cancellationToken) =>
        Ok(ApiResponseFactory.Success(await mediator.Send(new GetConnectionsQuery(userId), cancellationToken), "Connections."));

    [HttpDelete("{otherUserId:guid}")]
    public async Task<IActionResult> Remove(Guid otherUserId, CancellationToken cancellationToken)
    {
        var me = User.GetUserId();
        if (me == Guid.Empty) return Unauthorized();
        await mediator.Send(new RemoveConnectionCommand(me, otherUserId), cancellationToken);
        return NoContent();
    }
}
