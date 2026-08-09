using ConnectionService.Application.DTOs;
using MediatR;

namespace ConnectionService.Application.Commands.RequestConnection
{
    public sealed record RequestConnectionCommand(Guid RequesterUserId, Guid AddresseeUserId) : IRequest<ConnectionDto>;
   
    
}
