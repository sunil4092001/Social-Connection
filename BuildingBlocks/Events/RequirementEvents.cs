namespace BuildingBlocks.Events;

public sealed record ProfessionalInterestSubmittedEvent(
    Guid EventId,
    Guid RequirementId,
    Guid ResponseId,
    Guid CustomerId,
    Guid ProfessionalId,
    string RequirementTitle,
    string ProfessionalName,
    DateTime OccurredAtUtc);

public sealed record ProfessionalInterestDecisionEvent(
    Guid EventId,
    Guid RequirementId,
    Guid ResponseId,
    Guid CustomerId,
    Guid ProfessionalId,
    string RequirementTitle,
    string CustomerName,
    string Decision,
    DateTime OccurredAtUtc);

public sealed record MatchingProfessional(Guid ProfessionalId, int MatchScore);

public sealed record RequirementCreatedEvent(
    Guid EventId,
    Guid RequirementId,
    Guid CustomerId,
    string RequirementTitle,
    string ProfessionalType,
    string Location,
    IReadOnlyList<MatchingProfessional> MatchingProfessionals,
    DateTime OccurredAtUtc);

/// <summary>
/// Published when a customer cancels a requirement. Recipients are the professionals whose
/// response was still INTERESTED or ACCEPTED at the time of cancellation.
/// </summary>
public sealed record RequirementCancelledEvent(
    Guid EventId,
    Guid RequirementId,
    Guid CustomerId,
    string RequirementTitle,
    string CustomerName,
    IReadOnlyList<Guid> AffectedProfessionalIds,
    DateTime OccurredAtUtc);
