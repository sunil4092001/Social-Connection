using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using MediatR;

namespace ConnectionService.Application.Commands.RequestConnection
{
    public sealed class RequestConnectionCommandHandler(IConnectionRepository repository) : IRequestHandler<RequestConnectionCommand, ConnectionDto>
    {
        //public Task<ConnectionDto> Handle(RequestConnectionCommand request, CancellationToken cancellationToken)
        //{
        //    throw new NotImplementedException();
        //}
        public Task<ConnectionDto> Handle(RequestConnectionCommand request, CancellationToken cancellationToken) =>
       repository.RequestAsync(request.RequesterUserId, request.AddresseeUserId, cancellationToken);
    }


}
