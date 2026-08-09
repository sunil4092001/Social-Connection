using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using MediatR;

namespace ConnectionService.Application.Queries.GetConnections
{
    public class GetConnectionsQueryHandler(IConnectionRepository repository) : IRequestHandler<GetConnectionsQuery, IReadOnlyList<ConnectionDto>>
    {

        public Task<IReadOnlyList<ConnectionDto>> Handle(GetConnectionsQuery request, CancellationToken cancellationToken) =>
         repository.GetAcceptedAsync(request.UserId, cancellationToken);

    }

}
