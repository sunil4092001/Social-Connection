using MediatR;

namespace ConnectionService.Application.Commands.AcceptConnection
{
    public sealed record AcceptConnectionCommand(Guid RequesterUserId, Guid AddresseeUserId) : IRequest;
    
}
