using ConnectionService.Application.DTOs;
using MediatR;

namespace ConnectionService.Application.Queries.SentConnection
{
    public sealed record GetSentConnectionsQuery
    (
        Guid UserId
    ) : IRequest<IReadOnlyList<SentRequestDto>>;
    
}
