//using FluentValidation;
//using MediatR;

//namespace ConnectionService.Application;

////public sealed record ConnectionRequestDto(Guid RequesterUserId, Guid AddresseeUserId);
////public sealed record ConnectionDto(Guid UserId, Guid ConnectedUserId, string Status, DateTime CreatedAtUtc);
////public sealed record RequestConnectionCommand(Guid RequesterUserId, Guid AddresseeUserId) : IRequest<ConnectionDto>;
////public sealed record AcceptConnectionCommand(Guid RequesterUserId, Guid AddresseeUserId) : IRequest;
////public sealed record GetConnectionsQuery(Guid UserId) : IRequest<IReadOnlyList<ConnectionDto>>;
//AcceptConnectionCommandHandler
//public interface IConnectionRepository
//{
//    Task<ConnectionDto> RequestAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken);
//    Task AcceptAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken);
//    Task<IReadOnlyList<ConnectionDto>> GetAcceptedAsync(Guid userId, CancellationToken cancellationToken);
//}

////
//public sealed class RequestConnectionValidator : AbstractValidator<RequestConnectionCommand>
//{
//    public RequestConnectionValidator()
//    {
//        RuleFor(command => command.RequesterUserId).NotEmpty();
//        RuleFor(command => command.AddresseeUserId).NotEmpty();
//        RuleFor(command => command).Must(command => command.RequesterUserId != command.AddresseeUserId)
//            .WithMessage("Requester and addressee must be different users.");
//    }
//}
////
//public sealed class RequestConnectionCommandHandler(IConnectionRepository repository)
//    : IRequestHandler<RequestConnectionCommand, ConnectionDto>
//{
//    public Task<ConnectionDto> Handle(RequestConnectionCommand request, CancellationToken cancellationToken) =>
//        repository.RequestAsync(request.RequesterUserId, request.AddresseeUserId, cancellationToken);
//}
////
//public sealed class AcceptConnectionCommandHandler(IConnectionRepository repository)
//    : IRequestHandler<AcceptConnectionCommand>
//{
//    public Task Handle(AcceptConnectionCommand request, CancellationToken cancellationToken) =>
//        repository.AcceptAsync(request.RequesterUserId, request.AddresseeUserId, cancellationToken);
//}
////
//public sealed class GetConnectionsQueryHandler(IConnectionRepository repository)
//    : IRequestHandler<GetConnectionsQuery, IReadOnlyList<ConnectionDto>>
//{
//    public Task<IReadOnlyList<ConnectionDto>> Handle(GetConnectionsQuery request, CancellationToken cancellationToken) =>
//        repository.GetAcceptedAsync(request.UserId, cancellationToken);
//}
