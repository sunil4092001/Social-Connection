using BuildingBlocks.Common;
using ConnectionService.Application;
using ConnectionService.Application.Commands.AcceptConnection;
using ConnectionService.Application.Commands.RequestConnection;
using ConnectionService.Application.DTOs;
using ConnectionService.Application.Queries.GetConnections;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ConnectionService.Controllers;

[ApiController]
[Route("api/connections")]
public sealed class ConnectionsController(IMediator mediator) : ControllerBase

{
    //private User

    [HttpPost("request")]
    public async Task<ActionResult<ConnectionDto>> CreateRequest(ConnectionRequestDto request, CancellationToken cancellationToken)
    {
        var loginUserId = User.GetUserId();

        var connection = await mediator.Send(
            new RequestConnectionCommand(loginUserId, request.AddresseeUserId),
            cancellationToken);

        return Accepted(connection);
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept(ConnectionRequestDto request, CancellationToken cancellationToken)
    {
        var loginUserId = User.GetUserId();
        await mediator.Send(new AcceptConnectionCommand(loginUserId, request.AddresseeUserId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ConnectionDto>>> Get(Guid userId, CancellationToken cancellationToken)
    {
        var connections = await mediator.Send(new GetConnectionsQuery(userId), cancellationToken);
        return Ok(connections);
    }
    
    
    [HttpGet("sent-requests")]
    public async Task<ActionResult<IReadOnlyList<SentRequestDto>>> GetAllSentConnectionRequest(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var sentConnections = await mediator.Send(new GetConnectionsQuery(userId), cancellationToken);
        return Ok(sentConnections);
    }


}
