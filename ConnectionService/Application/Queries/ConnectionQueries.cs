using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using ConnectionService.Domain;
using FluentValidation;
using MediatR;

namespace ConnectionService.Application.Queries;

// ----- A user's connections -----

public sealed record GetConnectionsQuery(Guid UserId) : IRequest<IReadOnlyList<ConnectionUserDto>>;

public sealed class GetConnectionsValidator : AbstractValidator<GetConnectionsQuery>
{
    public GetConnectionsValidator() => RuleFor(query => query.UserId).NotEmpty();
}

public sealed class GetConnectionsQueryHandler(IConnectionRepository repository, IUserDirectoryClient directory)
    : IRequestHandler<GetConnectionsQuery, IReadOnlyList<ConnectionUserDto>>
{
    public async Task<IReadOnlyList<ConnectionUserDto>> Handle(GetConnectionsQuery request, CancellationToken cancellationToken) =>
        await UserNames.AttachAsync(directory, await repository.GetConnectionsAsync(request.UserId, cancellationToken), cancellationToken);
}

// ----- Pending requests sent to me -----

public sealed record GetReceivedRequestsQuery(Guid UserId) : IRequest<IReadOnlyList<ConnectionUserDto>>;

public sealed class GetReceivedRequestsValidator : AbstractValidator<GetReceivedRequestsQuery>
{
    public GetReceivedRequestsValidator() => RuleFor(query => query.UserId).NotEmpty();
}

public sealed class GetReceivedRequestsQueryHandler(IConnectionRepository repository, IUserDirectoryClient directory)
    : IRequestHandler<GetReceivedRequestsQuery, IReadOnlyList<ConnectionUserDto>>
{
    public async Task<IReadOnlyList<ConnectionUserDto>> Handle(GetReceivedRequestsQuery request, CancellationToken cancellationToken) =>
        await UserNames.AttachAsync(directory, await repository.GetPendingReceivedAsync(request.UserId, cancellationToken), cancellationToken);
}

// ----- Pending requests I sent -----

public sealed record GetSentConnectionsQuery(Guid UserId) : IRequest<IReadOnlyList<ConnectionUserDto>>;

public sealed class GetSentConnectionsValidator : AbstractValidator<GetSentConnectionsQuery>
{
    public GetSentConnectionsValidator() => RuleFor(query => query.UserId).NotEmpty();
}

public sealed class GetSentConnectionsQueryHandler(IConnectionRepository repository, IUserDirectoryClient directory)
    : IRequestHandler<GetSentConnectionsQuery, IReadOnlyList<ConnectionUserDto>>
{
    public async Task<IReadOnlyList<ConnectionUserDto>> Handle(GetSentConnectionsQuery request, CancellationToken cancellationToken) =>
        await UserNames.AttachAsync(directory, await repository.GetPendingSentAsync(request.UserId, cancellationToken), cancellationToken);
}

// ----- How I relate to another user (profile page button) -----

public sealed record GetConnectionStatusQuery(Guid UserId, Guid OtherUserId) : IRequest<ConnectionStatusDto>;

public sealed class GetConnectionStatusValidator : AbstractValidator<GetConnectionStatusQuery>
{
    public GetConnectionStatusValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.OtherUserId).NotEmpty();
    }
}

public sealed class GetConnectionStatusQueryHandler(IConnectionRepository repository)
    : IRequestHandler<GetConnectionStatusQuery, ConnectionStatusDto>
{
    public async Task<ConnectionStatusDto> Handle(GetConnectionStatusQuery request, CancellationToken cancellationToken)
    {
        var status = await repository.GetStatusAsync(request.UserId, request.OtherUserId, cancellationToken);
        return request.UserId == request.OtherUserId ? status with { Status = RelationshipStatus.Self, MutualConnectionCount = 0 } : status;
    }
}

// ----- Mutual connections -----

public sealed record GetMutualConnectionsQuery(Guid UserId, Guid OtherUserId) : IRequest<IReadOnlyList<ConnectionUserDto>>;

public sealed class GetMutualConnectionsValidator : AbstractValidator<GetMutualConnectionsQuery>
{
    public GetMutualConnectionsValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.OtherUserId).NotEmpty();
    }
}

public sealed class GetMutualConnectionsQueryHandler(IConnectionRepository repository, IUserDirectoryClient directory)
    : IRequestHandler<GetMutualConnectionsQuery, IReadOnlyList<ConnectionUserDto>>
{
    public async Task<IReadOnlyList<ConnectionUserDto>> Handle(GetMutualConnectionsQuery request, CancellationToken cancellationToken)
    {
        var ids = await repository.GetMutualConnectionsAsync(request.UserId, request.OtherUserId, cancellationToken);
        var names = await directory.GetDisplayNamesAsync(ids, cancellationToken);
        return ids.Select(id => new ConnectionUserDto(id, names.GetValueOrDefault(id, UserNames.Fallback), default)).ToList();
    }
}
