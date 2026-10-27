using BuildingBlocks.Exceptions;

namespace RequirementService.Domain;

public enum RequirementStatus { OPEN, IN_PROGRESS, COMPLETED, CANCELLED }
public enum ResponseStatus { INTERESTED, ACCEPTED, REJECTED }

/// <summary>
/// A customer's construction job. All status changes go through the methods below so the workflow rules
/// live in one place:
///   OPEN --accept a response--> IN_PROGRESS --complete--> COMPLETED
///   OPEN / IN_PROGRESS --cancel--> CANCELLED
/// </summary>
public sealed class Requirement
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProfessionalType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public string? ExpectedDuration { get; set; }
    public RequirementStatus Status { get; set; } = RequirementStatus.OPEN;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Optimistic concurrency token, mapped to PostgreSQL's xmin system column.</summary>
    public uint Version { get; set; }
    public ICollection<RequirementSkill> RequiredSkills { get; set; } = new List<RequirementSkill>();
    public ICollection<RequirementResponse> Responses { get; set; } = new List<RequirementResponse>();

    public bool IsOwnedBy(Guid userId) => CustomerId == userId;

    public void EnsureOwner(Guid customerId)
    {
        if (!IsOwnedBy(customerId)) throw new ForbiddenException("Only the requirement owner can perform this action.");
    }

    public void UpdateDetails(string title, string description, string professionalType, IEnumerable<Guid> requiredSkillIds,
        string location, DateOnly? startDate, string? expectedDuration, DateTime now)
    {
        EnsureStatus(RequirementStatus.OPEN, "Only open requirements can be edited.");
        var normalizedType = professionalType.Trim().ToUpperInvariant();
        if (normalizedType != ProfessionalType && Responses.Count > 0)
            throw new ConflictException("The professional type cannot be changed after professionals have responded.");

        Title = title.Trim();
        Description = description.Trim();
        ProfessionalType = normalizedType;
        Location = location.Trim();
        StartDate = startDate;
        ExpectedDuration = string.IsNullOrWhiteSpace(expectedDuration) ? null : expectedDuration.Trim();
        RequiredSkills.Clear();
        foreach (var skillId in requiredSkillIds.Distinct())
            RequiredSkills.Add(new RequirementSkill { RequirementId = Id, SkillId = skillId });
        UpdatedAtUtc = now;
    }

    public RequirementResponse AddResponse(Guid professionalId, string? message, DateTime now)
    {
        EnsureStatus(RequirementStatus.OPEN, "This requirement is no longer accepting responses.");
        if (IsOwnedBy(professionalId)) throw new ConflictException("You cannot respond to your own requirement.");
        if (Responses.Any(response => response.ProfessionalId == professionalId))
            throw new ConflictException("You have already responded to this requirement.");

        var response = new RequirementResponse
        {
            Id = Guid.NewGuid(),
            RequirementId = Id,
            ProfessionalId = professionalId,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            CreatedAtUtc = now
        };
        Responses.Add(response);
        UpdatedAtUtc = now;
        return response;
    }

    /// <summary>
    /// Hires the professional behind <paramref name="responseId"/>: the requirement moves to IN_PROGRESS and every
    /// other pending response is rejected. Returns the responses that were rejected automatically.
    /// </summary>
    public IReadOnlyList<RequirementResponse> AcceptResponse(Guid responseId, DateTime now)
    {
        EnsureStatus(RequirementStatus.OPEN, "A professional can only be selected while the requirement is open.");
        var accepted = GetPendingResponse(responseId);
        accepted.Decide(ResponseStatus.ACCEPTED, now);

        var autoRejected = Responses
            .Where(response => response.Id != responseId && response.Status == ResponseStatus.INTERESTED)
            .ToList();
        foreach (var response in autoRejected) response.Decide(ResponseStatus.REJECTED, now);

        Status = RequirementStatus.IN_PROGRESS;
        UpdatedAtUtc = now;
        return autoRejected;
    }

    public RequirementResponse RejectResponse(Guid responseId, DateTime now)
    {
        EnsureStatus(RequirementStatus.OPEN, "Responses can only be rejected while the requirement is open.");
        var response = GetPendingResponse(responseId);
        response.Decide(ResponseStatus.REJECTED, now);
        UpdatedAtUtc = now;
        return response;
    }

    /// <summary>Cancels the requirement and returns the professionals who should be told about it.</summary>
    public IReadOnlyList<Guid> Cancel(DateTime now)
    {
        if (Status is not (RequirementStatus.OPEN or RequirementStatus.IN_PROGRESS))
            throw new ConflictException($"A {Status} requirement cannot be cancelled.");

        Status = RequirementStatus.CANCELLED;
        UpdatedAtUtc = now;
        return Responses
            .Where(response => response.Status is ResponseStatus.INTERESTED or ResponseStatus.ACCEPTED)
            .Select(response => response.ProfessionalId)
            .Distinct()
            .ToList();
    }

    public void Complete(DateTime now)
    {
        EnsureStatus(RequirementStatus.IN_PROGRESS, "Only a requirement that is in progress can be completed.");
        Status = RequirementStatus.COMPLETED;
        UpdatedAtUtc = now;
    }

    private RequirementResponse GetPendingResponse(Guid responseId)
    {
        var response = Responses.FirstOrDefault(item => item.Id == responseId)
            ?? throw new KeyNotFoundException("Response was not found.");
        if (response.Status != ResponseStatus.INTERESTED)
            throw new ConflictException("This response has already been decided.");
        return response;
    }

    private void EnsureStatus(RequirementStatus expected, string message)
    {
        if (Status != expected) throw new ConflictException(message);
    }
}

public sealed class RequirementSkill
{
    public Guid RequirementId { get; set; }
    public Guid SkillId { get; set; }
    public Requirement Requirement { get; set; } = null!;
}

public sealed class RequirementResponse
{
    public Guid Id { get; set; }
    public Guid RequirementId { get; set; }
    public Guid ProfessionalId { get; set; }
    public string? Message { get; set; }
    public ResponseStatus Status { get; set; } = ResponseStatus.INTERESTED;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; set; }
    public Requirement Requirement { get; set; } = null!;

    internal void Decide(ResponseStatus decision, DateTime now)
    {
        Status = decision;
        DecidedAtUtc = now;
    }
}
