using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ConnectionService.Application.Queries;

// ----- People you may know -----

public sealed record GetConnectionSuggestionsQuery(Guid UserId, int Limit) : IRequest<IReadOnlyList<ConnectionSuggestionDto>>;

public sealed class GetConnectionSuggestionsValidator : AbstractValidator<GetConnectionSuggestionsQuery>
{
    public GetConnectionSuggestionsValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.Limit).InclusiveBetween(1, 50);
    }
}

public sealed class GetConnectionSuggestionsQueryHandler(IConnectionRepository repository, IUserDirectoryClient directory)
    : IRequestHandler<GetConnectionSuggestionsQuery, IReadOnlyList<ConnectionSuggestionDto>>
{
    public async Task<IReadOnlyList<ConnectionSuggestionDto>> Handle(GetConnectionSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var suggestions = await repository.GetSuggestionsAsync(request.UserId, request.Limit, cancellationToken);
        if (suggestions.Count == 0) return [];

        var names = await directory.GetDisplayNamesAsync(suggestions.Select(s => s.UserId), cancellationToken);
        // Users without a profile (e.g. deleted accounts) are not worth suggesting.
        return suggestions
            .Where(s => names.ContainsKey(s.UserId))
            .Select(s => new ConnectionSuggestionDto(s.UserId, names[s.UserId], s.MutualConnectionCount))
            .ToList();
    }
}

// ----- Network degree of a list of users (used by RequirementService matching and the professional directory) -----

public sealed record GetNetworkDegreesQuery(Guid UserId, IReadOnlyList<Guid> CandidateIds) : IRequest<IReadOnlyList<NetworkDegreeDto>>;

public sealed class GetNetworkDegreesValidator : AbstractValidator<GetNetworkDegreesQuery>
{
    public const int MaxCandidates = 200;

    public GetNetworkDegreesValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.CandidateIds).NotNull().Must(ids => ids.Count <= MaxCandidates)
            .WithMessage($"At most {MaxCandidates} users can be checked at once.");
    }
}

public sealed class GetNetworkDegreesQueryHandler(IConnectionRepository repository)
    : IRequestHandler<GetNetworkDegreesQuery, IReadOnlyList<NetworkDegreeDto>>
{
    public Task<IReadOnlyList<NetworkDegreeDto>> Handle(GetNetworkDegreesQuery request, CancellationToken cancellationToken) =>
        repository.GetDegreesAsync(request.UserId, request.CandidateIds, cancellationToken);
}
