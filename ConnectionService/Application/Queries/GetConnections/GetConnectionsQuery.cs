using ConnectionService.Application.DTOs;
using MediatR;

namespace ConnectionService.Application.Queries.GetConnections
{
    public sealed record GetConnectionsQuery(Guid UserId) : IRequest<IReadOnlyList<ConnectionDto>>;
    
   
}
