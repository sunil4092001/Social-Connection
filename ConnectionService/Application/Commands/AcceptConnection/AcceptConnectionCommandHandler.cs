using ConnectionService.Application.Interfaces;
using MediatR;

namespace ConnectionService.Application.Commands.AcceptConnection
{
    public sealed class AcceptConnectionCommandHandler(IConnectionRepository repository) : IRequestHandler<AcceptConnectionCommand>
    {
        //public Task Handle(AcceptConnectionCommand request, CancellationToken cancellationToken)
        //{
        //    throw new NotImplementedException();
        //}

        public Task Handle(AcceptConnectionCommand request, CancellationToken cancellationToken) =>
        repository.AcceptAsync(request.RequesterUserId, request.AddresseeUserId, cancellationToken);

    }
}
