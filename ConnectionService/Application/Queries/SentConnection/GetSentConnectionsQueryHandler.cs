using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using MediatR;

namespace ConnectionService.Application.Queries.SentConnection
{
    public sealed class GetSentConnectionsQueryHandler(IConnectionRepository repository) : IRequestHandler<GetSentConnectionsQuery, IReadOnlyList<SentRequestDto>>
    {
        public async Task<IReadOnlyList<SentRequestDto>> Handle(GetSentConnectionsQuery request, CancellationToken cancellationToken)
        {
            return await repository.GetAllSentConnectionRequestAsync(
             request.UserId,
             cancellationToken);
        }
    }
}
