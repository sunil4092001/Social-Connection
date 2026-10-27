using BuildingBlocks.Events;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Messaging.Outbox;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using RequirementService.Domain;
using RequirementService.Infrastructure;

namespace RequirementService.Application;

// ---------------------------------------------------------------------------------------------
// Requests and DTOs
// ---------------------------------------------------------------------------------------------

public interface IRequirementDetails
{
    string Title { get; }
    string Description { get; }
    string ProfessionalType { get; }
    IReadOnlyList<Guid> RequiredSkillIds { get; }
    string Location { get; }
    DateOnly? StartDate { get; }
    string? ExpectedDuration { get; }
}

public sealed record CreateRequirementRequest(string Title, string Description, string ProfessionalType, IReadOnlyList<Guid> RequiredSkillIds, string Location, DateOnly? StartDate, string? ExpectedDuration) : IRequirementDetails;
/// <summary>Status is intentionally not editable here; it only changes through accept / complete / cancel.</summary>
public sealed record UpdateRequirementRequest(string Title, string Description, string ProfessionalType, IReadOnlyList<Guid> RequiredSkillIds, string Location, DateOnly? StartDate, string? ExpectedDuration) : IRequirementDetails;
public sealed record RespondToRequirementRequest(string? Message);
public sealed record DecideResponseRequest(ResponseStatus Status);

public sealed record RequirementResponseDto(Guid Id, Guid ProfessionalId, string? Message, ResponseStatus Status, DateTime CreatedAtUtc, DateTime? DecidedAtUtc = null, string? ProfessionalName = null);
/// <summary>
/// Responses contain only what the viewer may see: all of them for the owner, the viewer's own response for a
/// professional, none for anybody else. ResponseCount is always the total.
/// </summary>
public sealed record RequirementDto(Guid Id, Guid CustomerId, string Title, string Description, string ProfessionalType, IReadOnlyList<Guid> RequiredSkillIds, string Location, DateOnly? StartDate, string? ExpectedDuration, RequirementStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IReadOnlyList<RequirementResponseDto> Responses, int ResponseCount);
/// <summary>NetworkDegree: 1 when the professional is connected to the customer, 2 when they share a connection, otherwise null.</summary>
public sealed record RecommendedRequirementDto(Guid Id, Guid CustomerId, string Title, string Description, string ProfessionalType, IReadOnlyList<Guid> RequiredSkillIds, string Location, DateOnly? StartDate, string? ExpectedDuration, RequirementStatus Status, DateTime CreatedAtUtc, decimal MatchScore, int? NetworkDegree = null);

// ---------------------------------------------------------------------------------------------
// Commands and queries
// ---------------------------------------------------------------------------------------------

public sealed record CreateRequirementCommand(Guid CustomerId, CreateRequirementRequest Request) : IRequest<RequirementDto>;
public sealed record UpdateRequirementCommand(Guid CustomerId, Guid RequirementId, UpdateRequirementRequest Request) : IRequest<RequirementDto>;
public sealed record CancelRequirementCommand(Guid CustomerId, Guid RequirementId) : IRequest;
public sealed record CompleteRequirementCommand(Guid CustomerId, Guid RequirementId) : IRequest<RequirementDto>;
public sealed record RespondToRequirementCommand(Guid ProfessionalId, string ProfessionalType, Guid RequirementId, string? Message) : IRequest<RequirementResponseDto>;
public sealed record DecideResponseCommand(Guid CustomerId, Guid RequirementId, Guid ResponseId, ResponseStatus Status) : IRequest<RequirementResponseDto>;

public sealed record GetRequirementQuery(Guid RequirementId, Guid ViewerId) : IRequest<RequirementDto?>;
public sealed record GetRequirementsQuery(Guid ViewerId, Guid? CustomerId, string? ProfessionalType, string? Location, RequirementStatus? Status) : IRequest<IReadOnlyList<RequirementDto>>;
public sealed record GetRecommendedRequirementsQuery(ProfessionalMatchProfile Professional) : IRequest<IReadOnlyList<RecommendedRequirementDto>>;
public sealed record GetProfessionalResponsesQuery(Guid ProfessionalId) : IRequest<IReadOnlyList<RequirementDto>>;
public sealed record GetRequirementResponsesQuery(Guid CustomerId, Guid RequirementId) : IRequest<IReadOnlyList<RequirementResponseDto>>;

// ---------------------------------------------------------------------------------------------
// Ports implemented in Infrastructure
// ---------------------------------------------------------------------------------------------

public interface IRequirementRepository
{
    void Add(Requirement requirement);
    Task<Requirement?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Requirement>> SearchAsync(Guid? customerId, string? professionalType, string? location, RequirementStatus? status, CancellationToken cancellationToken);
    /// <summary>Open requirements of one trade that <paramref name="excludeRespondedBy"/> has not responded to yet.</summary>
    Task<IReadOnlyList<Requirement>> SearchOpenByProfessionalTypeAsync(string professionalType, Guid excludeRespondedBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<Requirement>> SearchByProfessionalIdAsync(Guid professionalId, CancellationToken cancellationToken);
    /// <summary>Saves pending changes and outbox events atomically. Translates conflicts into ConflictException.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

// ---------------------------------------------------------------------------------------------
// Validators (one per command and query)
// ---------------------------------------------------------------------------------------------

internal sealed class RequirementDetailsValidator<T> : AbstractValidator<T> where T : IRequirementDetails
{
    public RequirementDetailsValidator(bool requireUpcomingStartDate)
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ProfessionalType).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ExpectedDuration).MaximumLength(100);
        RuleFor(x => x.RequiredSkillIds).NotNull().Must(ids => ids.Count <= 20).WithMessage("At most 20 skills can be requested.");
        RuleForEach(x => x.RequiredSkillIds).NotEmpty();
        if (requireUpcomingStartDate)
        {
            // One day of tolerance for time zones between the browser and the server.
            RuleFor(x => x.StartDate)
                .Must(date => date is null || date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
                .WithMessage("Start date cannot be in the past.");
        }
    }
}

public sealed class CreateRequirementValidator : AbstractValidator<CreateRequirementCommand>
{
    public CreateRequirementValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new RequirementDetailsValidator<CreateRequirementRequest>(requireUpcomingStartDate: true));
    }
}

public sealed class UpdateRequirementValidator : AbstractValidator<UpdateRequirementCommand>
{
    public UpdateRequirementValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.RequirementId).NotEmpty();
        RuleFor(x => x.Request).NotNull().SetValidator(new RequirementDetailsValidator<UpdateRequirementRequest>(requireUpcomingStartDate: false));
    }
}

public sealed class CancelRequirementValidator : AbstractValidator<CancelRequirementCommand>
{
    public CancelRequirementValidator() { RuleFor(x => x.CustomerId).NotEmpty(); RuleFor(x => x.RequirementId).NotEmpty(); }
}

public sealed class CompleteRequirementValidator : AbstractValidator<CompleteRequirementCommand>
{
    public CompleteRequirementValidator() { RuleFor(x => x.CustomerId).NotEmpty(); RuleFor(x => x.RequirementId).NotEmpty(); }
}

public sealed class RespondToRequirementValidator : AbstractValidator<RespondToRequirementCommand>
{
    public RespondToRequirementValidator()
    {
        RuleFor(x => x.ProfessionalId).NotEmpty();
        RuleFor(x => x.ProfessionalType).NotEmpty();
        RuleFor(x => x.RequirementId).NotEmpty();
        RuleFor(x => x.Message).MaximumLength(1000);
    }
}

public sealed class DecideResponseValidator : AbstractValidator<DecideResponseCommand>
{
    public DecideResponseValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.RequirementId).NotEmpty();
        RuleFor(x => x.ResponseId).NotEmpty();
        RuleFor(x => x.Status).Must(status => status is ResponseStatus.ACCEPTED or ResponseStatus.REJECTED)
            .WithMessage("Response status must be ACCEPTED or REJECTED.");
    }
}

public sealed class GetRequirementValidator : AbstractValidator<GetRequirementQuery>
{
    public GetRequirementValidator() => RuleFor(x => x.RequirementId).NotEmpty();
}

public sealed class GetRequirementsValidator : AbstractValidator<GetRequirementsQuery>
{
    public GetRequirementsValidator()
    {
        RuleFor(x => x.ProfessionalType).MaximumLength(80);
        RuleFor(x => x.Location).MaximumLength(120);
    }
}

public sealed class GetRecommendedRequirementsValidator : AbstractValidator<GetRecommendedRequirementsQuery>
{
    public GetRecommendedRequirementsValidator()
    {
        RuleFor(x => x.Professional).NotNull();
        RuleFor(x => x.Professional.Id).NotEmpty();
        RuleFor(x => x.Professional.ProfessionalType).NotEmpty();
    }
}

public sealed class GetProfessionalResponsesValidator : AbstractValidator<GetProfessionalResponsesQuery>
{
    public GetProfessionalResponsesValidator() => RuleFor(x => x.ProfessionalId).NotEmpty();
}

public sealed class GetRequirementResponsesValidator : AbstractValidator<GetRequirementResponsesQuery>
{
    public GetRequirementResponsesValidator() { RuleFor(x => x.CustomerId).NotEmpty(); RuleFor(x => x.RequirementId).NotEmpty(); }
}

// ---------------------------------------------------------------------------------------------
// Command handlers
// ---------------------------------------------------------------------------------------------

public sealed class CreateRequirementHandler(
    IRequirementRepository repo,
    IUserProfileClient profiles,
    IRequirementMatchingService matchingService,
    IOptions<RequirementNotificationOptions> notificationOptions,
    IEventOutbox outbox,
    IConnectionNetworkClient network,
    ILogger<CreateRequirementHandler> logger) : IRequestHandler<CreateRequirementCommand, RequirementDto>
{
    public async Task<RequirementDto> Handle(CreateRequirementCommand c, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var r = new Requirement
        {
            Id = Guid.NewGuid(),
            CustomerId = c.CustomerId,
            Status = RequirementStatus.OPEN,
            CreatedAtUtc = now
        };
        r.UpdateDetails(c.Request.Title, c.Request.Description, c.Request.ProfessionalType, c.Request.RequiredSkillIds,
            c.Request.Location, c.Request.StartDate, c.Request.ExpectedDuration, now);

        // Matching uses UserService. If it is unavailable the requirement is still created, just without notifications.
        IReadOnlyList<MatchingProfessional> matches = [];
        try
        {
            var candidates = await profiles.SearchProfessionalsAsync(r.ProfessionalType, ct);
            // Trust bonus: professionals in the customer's network rank higher (ConnectionService, Neo4j).
            var degrees = await network.GetDegreesAsync(candidates.Select(candidate => candidate.Id), ct);
            matches = matchingService.FindMatches(r, candidates, notificationOptions.Value, degrees);
            logger.LogInformation("Requirement {RequirementId} matched {CandidateCount} professionals; selected {RecipientCount}", r.Id, candidates.Count, matches.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Matching for requirement {RequirementId} failed; it will be created without match notifications", r.Id);
        }

        repo.Add(r);
        if (matches.Count > 0)
        {
            outbox.Add(EventTopics.RequirementCreated, r.Id.ToString(), new RequirementCreatedEvent(
                Guid.NewGuid(), r.Id, r.CustomerId, r.Title, r.ProfessionalType, r.Location, matches, now));
        }
        await repo.SaveChangesAsync(ct);
        return r.ToDto(c.CustomerId);
    }
}

public sealed class UpdateRequirementHandler(IRequirementRepository repo, IUserProfileClient profiles) : IRequestHandler<UpdateRequirementCommand, RequirementDto>
{
    public async Task<RequirementDto> Handle(UpdateRequirementCommand c, CancellationToken ct)
    {
        var r = await repo.GetAsync(c.RequirementId, ct) ?? throw new KeyNotFoundException("Requirement was not found.");
        r.EnsureOwner(c.CustomerId);
        r.UpdateDetails(c.Request.Title, c.Request.Description, c.Request.ProfessionalType, c.Request.RequiredSkillIds,
            c.Request.Location, c.Request.StartDate, c.Request.ExpectedDuration, DateTime.UtcNow);
        await repo.SaveChangesAsync(ct);
        return r.ToDto(c.CustomerId, await ProfileLookups.ResponseNamesAsync(profiles, [r], ct));
    }
}

public sealed class CancelRequirementHandler(IRequirementRepository repo, IUserProfileClient profiles, IEventOutbox outbox) : IRequestHandler<CancelRequirementCommand>
{
    public async Task Handle(CancelRequirementCommand c, CancellationToken ct)
    {
        var r = await repo.GetAsync(c.RequirementId, ct) ?? throw new KeyNotFoundException("Requirement was not found.");
        r.EnsureOwner(c.CustomerId);
        var now = DateTime.UtcNow;
        var affectedProfessionals = r.Cancel(now);

        if (affectedProfessionals.Count > 0)
        {
            var customerName = await ProfileLookups.DisplayNameOrDefaultAsync(profiles, c.CustomerId, "The customer", ct);
            outbox.Add(EventTopics.RequirementCancelled, r.Id.ToString(), new RequirementCancelledEvent(
                Guid.NewGuid(), r.Id, r.CustomerId, r.Title, customerName, affectedProfessionals, now));
        }
        await repo.SaveChangesAsync(ct);
    }
}

public sealed class CompleteRequirementHandler(IRequirementRepository repo, IUserProfileClient profiles) : IRequestHandler<CompleteRequirementCommand, RequirementDto>
{
    public async Task<RequirementDto> Handle(CompleteRequirementCommand c, CancellationToken ct)
    {
        var r = await repo.GetAsync(c.RequirementId, ct) ?? throw new KeyNotFoundException("Requirement was not found.");
        r.EnsureOwner(c.CustomerId);
        r.Complete(DateTime.UtcNow);
        await repo.SaveChangesAsync(ct);
        return r.ToDto(c.CustomerId, await ProfileLookups.ResponseNamesAsync(profiles, [r], ct));
    }
}

public sealed class RespondToRequirementHandler(
    IRequirementRepository repo,
    IUserProfileClient profiles,
    IEventOutbox outbox) : IRequestHandler<RespondToRequirementCommand, RequirementResponseDto>
{
    public async Task<RequirementResponseDto> Handle(RespondToRequirementCommand c, CancellationToken ct)
    {
        var r = await repo.GetAsync(c.RequirementId, ct) ?? throw new KeyNotFoundException("Requirement was not found.");
        if (!string.Equals(r.ProfessionalType, c.ProfessionalType.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException($"This requirement is for {r.ProfessionalType} professionals.");

        var now = DateTime.UtcNow;
        var response = r.AddResponse(c.ProfessionalId, c.Message, now);
        var professionalName = await ProfileLookups.DisplayNameOrDefaultAsync(profiles, c.ProfessionalId, "A construction professional", ct);

        outbox.Add(EventTopics.ProfessionalInterestSubmitted, r.Id.ToString(), new ProfessionalInterestSubmittedEvent(
            Guid.NewGuid(), r.Id, response.Id, r.CustomerId, c.ProfessionalId, r.Title, professionalName, now));
        await repo.SaveChangesAsync(ct);
        return response.ToDto(professionalName);
    }
}

public sealed class DecideResponseHandler(
    IRequirementRepository repo,
    IUserProfileClient profiles,
    IEventOutbox outbox) : IRequestHandler<DecideResponseCommand, RequirementResponseDto>
{
    public async Task<RequirementResponseDto> Handle(DecideResponseCommand c, CancellationToken ct)
    {
        var r = await repo.GetAsync(c.RequirementId, ct) ?? throw new KeyNotFoundException("Requirement was not found.");
        r.EnsureOwner(c.CustomerId);
        var now = DateTime.UtcNow;

        // Every professional whose response changes gets exactly one decision event.
        List<RequirementResponse> decided;
        if (c.Status == ResponseStatus.ACCEPTED)
        {
            var autoRejected = r.AcceptResponse(c.ResponseId, now);
            decided = [r.Responses.Single(response => response.Id == c.ResponseId), .. autoRejected];
        }
        else
        {
            decided = [r.RejectResponse(c.ResponseId, now)];
        }

        var customerName = await ProfileLookups.DisplayNameOrDefaultAsync(profiles, c.CustomerId, "The customer", ct);
        foreach (var response in decided)
        {
            outbox.Add(EventTopics.ProfessionalInterestDecision, r.Id.ToString(), new ProfessionalInterestDecisionEvent(
                Guid.NewGuid(), r.Id, response.Id, r.CustomerId, response.ProfessionalId, r.Title, customerName, response.Status.ToString(), now));
        }
        await repo.SaveChangesAsync(ct);

        var names = await profiles.GetDisplayNamesAsync([decided[0].ProfessionalId], ct);
        return decided[0].ToDto(names.GetValueOrDefault(decided[0].ProfessionalId));
    }
}

// ---------------------------------------------------------------------------------------------
// Query handlers
// ---------------------------------------------------------------------------------------------

public sealed class GetRequirementHandler(IRequirementRepository repo, IUserProfileClient profiles) : IRequestHandler<GetRequirementQuery, RequirementDto?>
{
    public async Task<RequirementDto?> Handle(GetRequirementQuery q, CancellationToken ct)
    {
        var r = await repo.GetAsync(q.RequirementId, ct);
        if (r is null) return null;
        var names = r.IsOwnedBy(q.ViewerId) ? await ProfileLookups.ResponseNamesAsync(profiles, [r], ct) : null;
        return r.ToDto(q.ViewerId, names);
    }
}

public sealed class GetRequirementsHandler(IRequirementRepository repo, IUserProfileClient profiles) : IRequestHandler<GetRequirementsQuery, IReadOnlyList<RequirementDto>>
{
    public async Task<IReadOnlyList<RequirementDto>> Handle(GetRequirementsQuery q, CancellationToken ct)
    {
        var requirements = await repo.SearchAsync(q.CustomerId, q.ProfessionalType, q.Location, q.Status, ct);
        // Names are only needed when the customer is looking at their own requirements (the "mine" list).
        var names = q.CustomerId.HasValue ? await ProfileLookups.ResponseNamesAsync(profiles, requirements, ct) : null;
        return requirements.Select(x => x.ToDto(q.ViewerId, names)).ToList();
    }
}

public sealed class GetRecommendedRequirementsHandler(
    IRequirementRepository repo,
    IRequirementMatchingService matchingService,
    IConnectionNetworkClient network,
    IOptions<RequirementNotificationOptions> options)
    : IRequestHandler<GetRecommendedRequirementsQuery, IReadOnlyList<RecommendedRequirementDto>>
{
    public async Task<IReadOnlyList<RecommendedRequirementDto>> Handle(GetRecommendedRequirementsQuery query, CancellationToken ct)
    {
        var candidates = await repo.SearchOpenByProfessionalTypeAsync(query.Professional.ProfessionalType, query.Professional.Id, ct);
        // Jobs from customers in the professional's network rank higher.
        var customerDegrees = await network.GetDegreesAsync(candidates.Select(requirement => requirement.CustomerId), ct);
        return matchingService.FindRecommendedRequirements(query.Professional, candidates, options.Value.MinimumRecommendationScore, customerDegrees)
            .Select(match => new RecommendedRequirementDto(
                match.Requirement.Id,
                match.Requirement.CustomerId,
                match.Requirement.Title,
                match.Requirement.Description,
                match.Requirement.ProfessionalType,
                match.Requirement.RequiredSkills.Select(skill => skill.SkillId).ToArray(),
                match.Requirement.Location,
                match.Requirement.StartDate,
                match.Requirement.ExpectedDuration,
                match.Requirement.Status,
                match.Requirement.CreatedAtUtc,
                match.MatchScore,
                match.NetworkDegree))
            .ToArray();
    }
}

public sealed class GetProfessionalResponsesHandler(IRequirementRepository repo) : IRequestHandler<GetProfessionalResponsesQuery, IReadOnlyList<RequirementDto>>
{
    public async Task<IReadOnlyList<RequirementDto>> Handle(GetProfessionalResponsesQuery query, CancellationToken ct) =>
        (await repo.SearchByProfessionalIdAsync(query.ProfessionalId, ct)).Select(requirement => requirement.ToDto(query.ProfessionalId)).ToList();
}

public sealed class GetRequirementResponsesHandler(IRequirementRepository repo, IUserProfileClient profiles)
    : IRequestHandler<GetRequirementResponsesQuery, IReadOnlyList<RequirementResponseDto>>
{
    public async Task<IReadOnlyList<RequirementResponseDto>> Handle(GetRequirementResponsesQuery q, CancellationToken ct)
    {
        var r = await repo.GetAsync(q.RequirementId, ct) ?? throw new KeyNotFoundException("Requirement was not found.");
        r.EnsureOwner(q.CustomerId);
        return r.ToDto(q.CustomerId, await ProfileLookups.ResponseNamesAsync(profiles, [r], ct)).Responses;
    }
}

// ---------------------------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------------------------

internal static class ProfileLookups
{
    public static async Task<string> DisplayNameOrDefaultAsync(IUserProfileClient profiles, Guid userId, string fallback, CancellationToken ct)
    {
        try
        {
            return await profiles.GetDisplayNameAsync(userId, ct) ?? fallback;
        }
        catch (HttpRequestException)
        {
            // A missing name must never block the business operation.
            return fallback;
        }
    }

    public static Task<IReadOnlyDictionary<Guid, string>> ResponseNamesAsync(IUserProfileClient profiles, IEnumerable<Requirement> requirements, CancellationToken ct) =>
        profiles.GetDisplayNamesAsync(requirements.SelectMany(r => r.Responses).Select(response => response.ProfessionalId), ct);
}

public static class RequirementMapping
{
    public static RequirementDto ToDto(this Requirement x, Guid viewerId, IReadOnlyDictionary<Guid, string>? names = null)
    {
        var visibleResponses = x.IsOwnedBy(viewerId)
            ? x.Responses
            : x.Responses.Where(response => response.ProfessionalId == viewerId);

        return new(x.Id, x.CustomerId, x.Title, x.Description, x.ProfessionalType,
            x.RequiredSkills.Select(s => s.SkillId).ToList(), x.Location, x.StartDate, x.ExpectedDuration, x.Status,
            x.CreatedAtUtc, x.UpdatedAtUtc,
            visibleResponses
                .OrderByDescending(r => r.CreatedAtUtc)
                .Select(r => r.ToDto(names?.GetValueOrDefault(r.ProfessionalId)))
                .ToList(),
            x.Responses.Count);
    }

    public static RequirementResponseDto ToDto(this RequirementResponse x, string? professionalName = null) =>
        new(x.Id, x.ProfessionalId, x.Message, x.Status, x.CreatedAtUtc, x.DecidedAtUtc, professionalName);
}
