using ConnectionService.Application.DTOs;

namespace ConnectionService.Application.Interfaces
{
    public interface IConnectionRepository
    {
        Task<ConnectionDto> RequestAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken);
        Task<ConnectionDto> AcceptAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken);
        Task<IReadOnlyList<ConnectionDto>> GetAcceptedAsync(Guid userId, CancellationToken cancellationToken);
        Task<IReadOnlyList<SentRequestDto>> GetAllSentConnectionRequestAsync(Guid userId, CancellationToken cancellationToken);
    }
}
