using BuildingBlocks.Events;
using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ConnectionService.Application.Commands;

// In every command the signed-in user's id comes from the JWT (set by the controller), never from the request body.

// ----- Send a request -----

public sealed record RequestConnectionCommand(Guid RequesterUserId, Guid AddresseeUserId) : IRequest<ConnectionDto>;

public sealed class RequestConnectionValidator : AbstractValidator<RequestConnectionCommand>
{
    public RequestConnectionValidator()
    {
        RuleFor(command => command.RequesterUserId).NotEmpty();
        RuleFor(command => command.AddresseeUserId).NotEmpty();
        RuleFor(command => command).Must(command => command.RequesterUserId != command.AddresseeUserId)
            .WithMessage("You cannot send a connection request to yourself.");
    }
}

public sealed class RequestConnectionCommandHandler(IConnectionRepository repository, IUserDirectoryClient directory)
    : IRequestHandler<RequestConnectionCommand, ConnectionDto>
{
    public async Task<ConnectionDto> Handle(RequestConnectionCommand request, CancellationToken cancellationToken)
    {
        // The recipient must be a real BuildConnect user (UserService owns that knowledge).
        _ = await directory.GetDisplayNameAsync(request.AddresseeUserId, cancellationToken)
            ?? throw new KeyNotFoundException("User was not found.");
        var requesterName = await directory.GetDisplayNameAsync(request.RequesterUserId, cancellationToken) ?? UserNames.Fallback;

        var now = DateTime.UtcNow;
        var @event = OutboxEnvelope.For(EventTopics.ConnectionRequested, request.RequesterUserId, request.AddresseeUserId,
            new ConnectionRequestedEvent(Guid.NewGuid(), request.RequesterUserId, request.AddresseeUserId, requesterName, now), now);

        return await repository.RequestAsync(request.RequesterUserId, request.AddresseeUserId, @event, cancellationToken);
    }
}

// ----- Accept a request (only the addressee can) -----

public sealed record AcceptConnectionCommand(Guid RequesterUserId, Guid AddresseeUserId) : IRequest<ConnectionDto>;

public sealed class AcceptConnectionValidator : AbstractValidator<AcceptConnectionCommand>
{
    public AcceptConnectionValidator()
    {
        RuleFor(command => command.RequesterUserId).NotEmpty();
        RuleFor(command => command.AddresseeUserId).NotEmpty();
        RuleFor(command => command).Must(command => command.RequesterUserId != command.AddresseeUserId)
            .WithMessage("You cannot accept a request from yourself.");
    }
}

public sealed class AcceptConnectionCommandHandler(IConnectionRepository repository, IUserDirectoryClient directory)
    : IRequestHandler<AcceptConnectionCommand, ConnectionDto>
{
    public async Task<ConnectionDto> Handle(AcceptConnectionCommand request, CancellationToken cancellationToken)
    {
        var addresseeName = await directory.GetDisplayNameAsync(request.AddresseeUserId, cancellationToken) ?? UserNames.Fallback;
        var now = DateTime.UtcNow;
        var @event = OutboxEnvelope.For(EventTopics.ConnectionAccepted, request.RequesterUserId, request.AddresseeUserId,
            new ConnectionAcceptedEvent(Guid.NewGuid(), request.RequesterUserId, request.AddresseeUserId, addresseeName, now), now);

        // The query only matches requester -> signed-in user, so nobody else can accept this request.
        return await repository.AcceptAsync(request.RequesterUserId, request.AddresseeUserId, @event, cancellationToken)
            ?? throw new KeyNotFoundException("There is no pending connection request from this user.");
    }
}

// ----- Reject a request (only the addressee can; the requester is not notified) -----

public sealed record RejectConnectionCommand(Guid RequesterUserId, Guid AddresseeUserId) : IRequest;

public sealed class RejectConnectionValidator : AbstractValidator<RejectConnectionCommand>
{
    public RejectConnectionValidator()
    {
        RuleFor(command => command.RequesterUserId).NotEmpty();
        RuleFor(command => command.AddresseeUserId).NotEmpty();
    }
}

public sealed class RejectConnectionCommandHandler(IConnectionRepository repository) : IRequestHandler<RejectConnectionCommand>
{
    public async Task Handle(RejectConnectionCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.RejectAsync(request.RequesterUserId, request.AddresseeUserId, cancellationToken))
            throw new KeyNotFoundException("There is no pending connection request from this user.");
    }
}

// ----- Withdraw my own pending request -----

public sealed record WithdrawConnectionRequestCommand(Guid RequesterUserId, Guid AddresseeUserId) : IRequest;

public sealed class WithdrawConnectionRequestValidator : AbstractValidator<WithdrawConnectionRequestCommand>
{
    public WithdrawConnectionRequestValidator()
    {
        RuleFor(command => command.RequesterUserId).NotEmpty();
        RuleFor(command => command.AddresseeUserId).NotEmpty();
    }
}

public sealed class WithdrawConnectionRequestCommandHandler(IConnectionRepository repository) : IRequestHandler<WithdrawConnectionRequestCommand>
{
    public async Task Handle(WithdrawConnectionRequestCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.WithdrawAsync(request.RequesterUserId, request.AddresseeUserId, cancellationToken))
            throw new KeyNotFoundException("You have no pending connection request to this user.");
    }
}

// ----- Remove an existing connection -----

public sealed record RemoveConnectionCommand(Guid UserId, Guid OtherUserId) : IRequest;

public sealed class RemoveConnectionValidator : AbstractValidator<RemoveConnectionCommand>
{
    public RemoveConnectionValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.OtherUserId).NotEmpty();
    }
}

public sealed class RemoveConnectionCommandHandler(IConnectionRepository repository) : IRequestHandler<RemoveConnectionCommand>
{
    public async Task Handle(RemoveConnectionCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.RemoveAsync(request.UserId, request.OtherUserId, cancellationToken))
            throw new KeyNotFoundException("You are not connected with this user.");
    }
}
