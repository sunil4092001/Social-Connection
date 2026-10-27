using BuildingBlocks.Events;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Messaging.Outbox;
using RequirementService.Application;
using RequirementService.Domain;
using RequirementService.Infrastructure;

namespace LinkedIn.Tests;

/// <summary>Business rules of the requirement lifecycle (domain) and the events each handler stages.</summary>
public sealed class RequirementWorkflowTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);
    private readonly Guid customerId = Guid.NewGuid();

    // ----- Domain rules -----

    [Fact]
    public void Accepting_a_response_starts_work_and_rejects_other_pending_responses()
    {
        var requirement = CreateRequirement();
        var first = requirement.AddResponse(Guid.NewGuid(), "I can do it", Now);
        var second = requirement.AddResponse(Guid.NewGuid(), null, Now);
        var third = requirement.AddResponse(Guid.NewGuid(), null, Now);
        requirement.RejectResponse(third.Id, Now);

        var autoRejected = requirement.AcceptResponse(first.Id, Now);

        Assert.Equal(RequirementStatus.IN_PROGRESS, requirement.Status);
        Assert.Equal(ResponseStatus.ACCEPTED, first.Status);
        Assert.Equal(ResponseStatus.REJECTED, second.Status);
        Assert.Equal([second.Id], autoRejected.Select(r => r.Id));
        Assert.Equal(Now, first.DecidedAtUtc);
    }

    [Fact]
    public void A_decided_response_cannot_be_decided_again()
    {
        var requirement = CreateRequirement();
        var response = requirement.AddResponse(Guid.NewGuid(), null, Now);
        requirement.RejectResponse(response.Id, Now);

        Assert.Throws<ConflictException>(() => requirement.AcceptResponse(response.Id, Now));
    }

    [Fact]
    public void Only_one_professional_can_be_accepted()
    {
        var requirement = CreateRequirement();
        var first = requirement.AddResponse(Guid.NewGuid(), null, Now);
        var late = requirement.AddResponse(Guid.NewGuid(), null, Now);
        requirement.AcceptResponse(first.Id, Now);

        Assert.Throws<ConflictException>(() => requirement.AcceptResponse(late.Id, Now));
    }

    [Fact]
    public void Responses_are_refused_when_requirement_is_not_open()
    {
        var requirement = CreateRequirement();
        requirement.Cancel(Now);

        Assert.Throws<ConflictException>(() => requirement.AddResponse(Guid.NewGuid(), null, Now));
    }

    [Fact]
    public void Same_professional_cannot_respond_twice()
    {
        var requirement = CreateRequirement();
        var professionalId = Guid.NewGuid();
        requirement.AddResponse(professionalId, null, Now);

        Assert.Throws<ConflictException>(() => requirement.AddResponse(professionalId, null, Now));
    }

    [Fact]
    public void Owner_cannot_respond_to_own_requirement()
    {
        var requirement = CreateRequirement();

        Assert.Throws<ConflictException>(() => requirement.AddResponse(customerId, null, Now));
    }

    [Fact]
    public void Only_owner_passes_owner_check()
    {
        var requirement = CreateRequirement();

        Assert.Throws<ForbiddenException>(() => requirement.EnsureOwner(Guid.NewGuid()));
        requirement.EnsureOwner(customerId);
    }

    [Fact]
    public void Cancel_returns_professionals_still_involved_and_is_final()
    {
        var requirement = CreateRequirement();
        var interested = requirement.AddResponse(Guid.NewGuid(), null, Now);
        var rejected = requirement.AddResponse(Guid.NewGuid(), null, Now);
        requirement.RejectResponse(rejected.Id, Now);

        var affected = requirement.Cancel(Now);

        Assert.Equal(RequirementStatus.CANCELLED, requirement.Status);
        Assert.Equal([interested.ProfessionalId], affected);
        Assert.Throws<ConflictException>(() => requirement.Cancel(Now));
    }

    [Fact]
    public void Complete_is_only_allowed_while_in_progress()
    {
        var requirement = CreateRequirement();
        Assert.Throws<ConflictException>(() => requirement.Complete(Now));

        var response = requirement.AddResponse(Guid.NewGuid(), null, Now);
        requirement.AcceptResponse(response.Id, Now);
        requirement.Complete(Now);

        Assert.Equal(RequirementStatus.COMPLETED, requirement.Status);
        Assert.Throws<ConflictException>(() => requirement.Cancel(Now));
    }

    [Fact]
    public void Details_can_only_be_edited_while_open_and_trade_is_locked_after_responses()
    {
        var requirement = CreateRequirement();
        requirement.AddResponse(Guid.NewGuid(), null, Now);

        Assert.Throws<ConflictException>(() => requirement.UpdateDetails("T", "D", "PLUMBER", [], "Delhi", null, null, Now));

        requirement.UpdateDetails("New title", "D", "ELECTRICIAN", [], "Delhi", null, null, Now);
        Assert.Equal("New title", requirement.Title);

        requirement.Cancel(Now);
        Assert.Throws<ConflictException>(() => requirement.UpdateDetails("T", "D", "ELECTRICIAN", [], "Delhi", null, null, Now));
    }

    [Fact]
    public void Dto_shows_responses_only_to_owner_or_the_responding_professional()
    {
        var requirement = CreateRequirement();
        var professionalId = Guid.NewGuid();
        requirement.AddResponse(professionalId, null, Now);
        requirement.AddResponse(Guid.NewGuid(), null, Now);

        Assert.Equal(2, requirement.ToDto(customerId).Responses.Count);
        Assert.Single(requirement.ToDto(professionalId).Responses);
        Assert.Empty(requirement.ToDto(Guid.Empty).Responses);
        Assert.Equal(2, requirement.ToDto(Guid.Empty).ResponseCount);
    }

    [Fact]
    public void Recommendations_below_threshold_are_excluded()
    {
        var matching = new RequirementMatchingService();
        var requirement = CreateRequirement();
        requirement.RequiredSkills.Add(new RequirementSkill { SkillId = Guid.NewGuid() });
        // Same trade but no skill and no location overlap: 40 base + 10 available + 5 experience = 55.
        var professional = new ProfessionalMatchProfile(Guid.NewGuid(), "ELECTRICIAN", "Mumbai", 3, "AVAILABLE", []);

        Assert.Empty(matching.FindRecommendedRequirements(professional, [requirement]));
    }

    // ----- Handlers: what gets staged in the outbox -----

    [Fact]
    public async Task Respond_stages_interest_event_for_the_customer()
    {
        var requirement = CreateRequirement();
        var (repo, outbox, profiles) = CreateInfrastructure(requirement);
        var professionalId = Guid.NewGuid();
        profiles.Names[professionalId] = "Amit Kumar";

        var dto = await new RespondToRequirementHandler(repo, profiles, outbox)
            .Handle(new RespondToRequirementCommand(professionalId, "electrician", requirement.Id, "Available next week"), CancellationToken.None);

        var staged = Assert.Single(outbox.Events);
        Assert.Equal(EventTopics.ProfessionalInterestSubmitted, staged.Topic);
        var @event = Assert.IsType<ProfessionalInterestSubmittedEvent>(staged.Event);
        Assert.Equal(customerId, @event.CustomerId);
        Assert.Equal("Amit Kumar", @event.ProfessionalName);
        Assert.Equal(ResponseStatus.INTERESTED, dto.Status);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Respond_with_a_different_trade_is_forbidden()
    {
        var requirement = CreateRequirement();
        var (repo, outbox, profiles) = CreateInfrastructure(requirement);

        await Assert.ThrowsAsync<ForbiddenException>(() => new RespondToRequirementHandler(repo, profiles, outbox)
            .Handle(new RespondToRequirementCommand(Guid.NewGuid(), "PLUMBER", requirement.Id, null), CancellationToken.None));
        Assert.Empty(outbox.Events);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Accept_stages_one_decision_event_per_affected_professional()
    {
        var requirement = CreateRequirement();
        var chosen = requirement.AddResponse(Guid.NewGuid(), null, Now);
        var other = requirement.AddResponse(Guid.NewGuid(), null, Now);
        var (repo, outbox, profiles) = CreateInfrastructure(requirement);

        await new DecideResponseHandler(repo, profiles, outbox)
            .Handle(new DecideResponseCommand(customerId, requirement.Id, chosen.Id, ResponseStatus.ACCEPTED), CancellationToken.None);

        var decisions = outbox.Events.Select(e => Assert.IsType<ProfessionalInterestDecisionEvent>(e.Event)).ToList();
        Assert.Equal(2, decisions.Count);
        Assert.Contains(decisions, d => d.ProfessionalId == chosen.ProfessionalId && d.Decision == "ACCEPTED");
        Assert.Contains(decisions, d => d.ProfessionalId == other.ProfessionalId && d.Decision == "REJECTED");
        Assert.All(outbox.Events, e => Assert.Equal(requirement.Id.ToString(), e.Key));
        Assert.Equal(RequirementStatus.IN_PROGRESS, requirement.Status);
    }

    [Fact]
    public async Task Decision_by_non_owner_is_forbidden_and_stages_nothing()
    {
        var requirement = CreateRequirement();
        var response = requirement.AddResponse(Guid.NewGuid(), null, Now);
        var (repo, outbox, profiles) = CreateInfrastructure(requirement);

        await Assert.ThrowsAsync<ForbiddenException>(() => new DecideResponseHandler(repo, profiles, outbox)
            .Handle(new DecideResponseCommand(Guid.NewGuid(), requirement.Id, response.Id, ResponseStatus.ACCEPTED), CancellationToken.None));
        Assert.Empty(outbox.Events);
    }

    [Fact]
    public async Task Cancel_stages_cancelled_event_only_when_professionals_are_involved()
    {
        var withResponse = CreateRequirement();
        var response = withResponse.AddResponse(Guid.NewGuid(), null, Now);
        var (repo, outbox, profiles) = CreateInfrastructure(withResponse);

        await new CancelRequirementHandler(repo, profiles, outbox).Handle(new CancelRequirementCommand(customerId, withResponse.Id), CancellationToken.None);

        var @event = Assert.IsType<RequirementCancelledEvent>(Assert.Single(outbox.Events).Event);
        Assert.Equal([response.ProfessionalId], @event.AffectedProfessionalIds);

        var empty = CreateRequirement();
        var (emptyRepo, emptyOutbox, _) = CreateInfrastructure(empty);
        await new CancelRequirementHandler(emptyRepo, profiles, emptyOutbox).Handle(new CancelRequirementCommand(customerId, empty.Id), CancellationToken.None);
        Assert.Empty(emptyOutbox.Events);
    }

    [Fact]
    public void Decide_validator_rejects_interested_as_a_decision()
    {
        var result = new DecideResponseValidator().Validate(new DecideResponseCommand(customerId, Guid.NewGuid(), Guid.NewGuid(), ResponseStatus.INTERESTED));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_validator_rejects_start_date_in_the_past()
    {
        var request = new CreateRequirementRequest("Rewire flat", "Full rewiring", "ELECTRICIAN", [], "Gurgaon", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), null);

        Assert.False(new CreateRequirementValidator().Validate(new CreateRequirementCommand(customerId, request)).IsValid);
    }

    // ----- Helpers -----

    private Requirement CreateRequirement() => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        Title = "Rewire 2BHK flat",
        Description = "Complete rewiring",
        ProfessionalType = "ELECTRICIAN",
        Location = "Gurgaon",
        Status = RequirementStatus.OPEN
    };

    private static (InMemoryRequirementRepository, RecordingOutbox, NamedProfiles) CreateInfrastructure(Requirement requirement) =>
        (new InMemoryRequirementRepository(requirement), new RecordingOutbox(), new NamedProfiles());

    private sealed class InMemoryRequirementRepository(params Requirement[] requirements) : IRequirementRepository
    {
        private readonly Dictionary<Guid, Requirement> store = requirements.ToDictionary(r => r.Id);
        public int SaveCount { get; private set; }

        public void Add(Requirement requirement) => store[requirement.Id] = requirement;
        public Task<Requirement?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(store.GetValueOrDefault(id));
        public Task<IReadOnlyList<Requirement>> SearchAsync(Guid? customerId, string? professionalType, string? location, RequirementStatus? status, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Requirement>>(store.Values.ToList());
        public Task<IReadOnlyList<Requirement>> SearchOpenByProfessionalTypeAsync(string professionalType, Guid excludeRespondedBy, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Requirement>>(store.Values.ToList());
        public Task<IReadOnlyList<Requirement>> SearchByProfessionalIdAsync(Guid professionalId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Requirement>>(store.Values.ToList());
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
    }

    private sealed class RecordingOutbox : IEventOutbox
    {
        public List<(string Topic, string Key, object Event)> Events { get; } = [];
        public void Add<TEvent>(string topic, string key, TEvent @event) where TEvent : notnull => Events.Add((topic, key, @event));
    }

    private sealed class NamedProfiles : IUserProfileClient
    {
        public Dictionary<Guid, string> Names { get; } = [];
        public Task<bool> HasTypeAsync(Guid userId, string expectedType, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<ProfessionalMatchProfile?> GetProfessionalMatchProfileAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<ProfessionalMatchProfile?>(null);
        public Task<IReadOnlyList<ProfessionalMatchProfile>> SearchProfessionalsAsync(string professionalType, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProfessionalMatchProfile>>([]);
        public Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Names.GetValueOrDefault(userId));
        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(userIds.Where(Names.ContainsKey).ToDictionary(id => id, id => Names[id]));
    }
}
