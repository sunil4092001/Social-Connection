using System.Text.Json;
using BuildingBlocks.Events;
using BuildingBlocks.Exceptions;
using ConnectionService.Application;
using ConnectionService.Application.Commands;
using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;
using ConnectionService.Application.Queries;
using ConnectionService.Domain;
using NotificationService.Application;

namespace LinkedIn.Tests;

/// <summary>Connection request rules, handler behaviour and the notifications connection events produce.</summary>
public sealed class ConnectionWorkflowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Guid me = Guid.NewGuid();
    private readonly Guid other = Guid.NewGuid();

    // ----- Rules -----

    [Fact]
    public void New_request_is_allowed_when_there_is_no_relationship() =>
        ConnectionRules.EnsureCanRequest(new ConnectionState(false, null, false));

    [Fact]
    public void Request_after_a_rejection_is_allowed() =>
        ConnectionRules.EnsureCanRequest(new ConnectionState(false, RequestStatus.Rejected, false));

    [Theory]
    [InlineData(true, null, false)]                    // already connected
    [InlineData(false, RequestStatus.Pending, false)]  // duplicate request
    [InlineData(false, null, true)]                    // they already asked me
    public void Conflicting_requests_are_refused(bool connected, string? myRequest, bool theyRequested) =>
        Assert.Throws<ConflictException>(() => ConnectionRules.EnsureCanRequest(new ConnectionState(connected, myRequest, theyRequested)));

    [Fact]
    public void Self_connection_is_invalid() =>
        Assert.False(new RequestConnectionValidator().Validate(new RequestConnectionCommand(me, me)).IsValid);

    // ----- Handlers -----

    [Fact]
    public async Task Request_to_unknown_user_is_not_found_and_writes_nothing()
    {
        var repository = new RecordingRepository();
        var directory = new FakeDirectory();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new RequestConnectionCommandHandler(repository, directory)
            .Handle(new RequestConnectionCommand(me, other), CancellationToken.None));
        Assert.Null(repository.LastEvent);
    }

    [Fact]
    public async Task Request_stores_connection_requested_event_for_the_addressee()
    {
        var repository = new RecordingRepository();
        var directory = new FakeDirectory { [me] = "Priya Sharma", [other] = "Rajesh Kumar" };

        await new RequestConnectionCommandHandler(repository, directory).Handle(new RequestConnectionCommand(me, other), CancellationToken.None);

        Assert.Equal(EventTopics.ConnectionRequested, repository.LastEvent!.Topic);
        var @event = JsonSerializer.Deserialize<ConnectionRequestedEvent>(repository.LastEvent.Payload, Json)!;
        Assert.Equal(other, @event.AddresseeUserId);
        Assert.Equal("Priya Sharma", @event.RequesterName);
    }

    [Fact]
    public async Task Accept_without_pending_request_is_not_found()
    {
        var repository = new RecordingRepository { AcceptResult = null };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AcceptConnectionCommandHandler(repository, new FakeDirectory())
            .Handle(new AcceptConnectionCommand(other, me), CancellationToken.None));
    }

    [Fact]
    public async Task Reject_and_withdraw_without_pending_request_are_not_found()
    {
        var repository = new RecordingRepository { WriteResult = false };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new RejectConnectionCommandHandler(repository).Handle(new RejectConnectionCommand(other, me), CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new WithdrawConnectionRequestCommandHandler(repository).Handle(new WithdrawConnectionRequestCommand(me, other), CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new RemoveConnectionCommandHandler(repository).Handle(new RemoveConnectionCommand(me, other), CancellationToken.None));
    }

    [Fact]
    public async Task Status_of_my_own_profile_is_self()
    {
        var status = await new GetConnectionStatusQueryHandler(new RecordingRepository()).Handle(new GetConnectionStatusQuery(me, me), CancellationToken.None);

        Assert.Equal(RelationshipStatus.Self, status.Status);
    }

    [Fact]
    public async Task Lists_use_fallback_name_when_user_service_has_no_profile()
    {
        var repository = new RecordingRepository { Edges = [new ConnectionEdge(other, DateTime.UtcNow)] };

        var list = await new GetReceivedRequestsQueryHandler(repository, new FakeDirectory()).Handle(new GetReceivedRequestsQuery(me), CancellationToken.None);

        Assert.Equal("BuildConnect member", Assert.Single(list).DisplayName);
    }

    [Fact]
    public void Requested_and_accepted_events_for_a_pair_share_one_kafka_key()
    {
        var requested = OutboxEnvelope.For(EventTopics.ConnectionRequested, me, other, new { }, DateTime.UtcNow);
        var accepted = OutboxEnvelope.For(EventTopics.ConnectionAccepted, other, me, new { }, DateTime.UtcNow);

        Assert.Equal(requested.Key, accepted.Key);
    }

    // ----- Notifications produced by connection events -----

    [Fact]
    public void Connection_requested_notifies_the_addressee_and_links_to_the_requester()
    {
        var @event = new ConnectionRequestedEvent(Guid.NewGuid(), me, other, "Priya Sharma", DateTime.UtcNow);

        var command = Assert.Single(NotificationEventMapper.Map(EventTopics.ConnectionRequested, JsonSerializer.Serialize(@event, Json)));

        Assert.Equal(other, command.RecipientUserId);
        Assert.Equal(me, command.ReferenceId);
        Assert.Equal("ConnectionRequested", command.Type);
    }

    [Fact]
    public void Connection_accepted_notifies_the_requester()
    {
        var @event = new ConnectionAcceptedEvent(Guid.NewGuid(), me, other, "Rajesh Kumar", DateTime.UtcNow);

        var command = Assert.Single(NotificationEventMapper.Map(EventTopics.ConnectionAccepted, JsonSerializer.Serialize(@event, Json)));

        Assert.Equal(me, command.RecipientUserId);
        Assert.Contains("Rajesh Kumar", command.Message);
    }

    // ----- Network intelligence -----

    [Fact]
    public async Task Suggestions_get_names_and_skip_users_without_a_profile()
    {
        var known = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        var repository = new RecordingRepository { Suggestions = [new SuggestionEdge(known, 3), new SuggestionEdge(deleted, 2)] };
        var directory = new FakeDirectory { [known] = "Rakesh Verma" };

        var suggestions = await new GetConnectionSuggestionsQueryHandler(repository, directory).Handle(new GetConnectionSuggestionsQuery(me, 10), CancellationToken.None);

        var only = Assert.Single(suggestions);
        Assert.Equal("Rakesh Verma", only.DisplayName);
        Assert.Equal(3, only.MutualConnectionCount);
    }

    [Fact]
    public void Degree_lookup_is_bounded()
    {
        var tooMany = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();

        Assert.False(new GetNetworkDegreesValidator().Validate(new GetNetworkDegreesQuery(me, tooMany)).IsValid);
        Assert.False(new GetConnectionSuggestionsValidator().Validate(new GetConnectionSuggestionsQuery(me, 0)).IsValid);
    }

    // ----- Fakes -----

    private sealed class FakeDirectory : Dictionary<Guid, string>, IUserDirectoryClient
    {
        public Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(TryGetValue(userId, out var name) ? name : null);
        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(userIds.Where(ContainsKey).ToDictionary(id => id, id => this[id]));
    }

    private sealed class RecordingRepository : IConnectionRepository
    {
        public OutboxEnvelope? LastEvent { get; private set; }
        public ConnectionDto? AcceptResult { get; init; }
        public bool WriteResult { get; init; } = true;
        public IReadOnlyList<ConnectionEdge> Edges { get; init; } = [];
        public IReadOnlyList<SuggestionEdge> Suggestions { get; init; } = [];
        public IReadOnlyList<NetworkDegreeDto> Degrees { get; init; } = [];

        public Task<ConnectionDto> RequestAsync(Guid requesterUserId, Guid addresseeUserId, OutboxEnvelope @event, CancellationToken cancellationToken)
        {
            LastEvent = @event;
            return Task.FromResult(new ConnectionDto(requesterUserId, addresseeUserId, RequestStatus.Pending, DateTime.UtcNow, null));
        }
        public Task<ConnectionDto?> AcceptAsync(Guid requesterUserId, Guid addresseeUserId, OutboxEnvelope @event, CancellationToken cancellationToken)
        {
            if (AcceptResult is not null) LastEvent = @event;
            return Task.FromResult(AcceptResult);
        }
        public Task<bool> RejectAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken) => Task.FromResult(WriteResult);
        public Task<bool> WithdrawAsync(Guid requesterUserId, Guid addresseeUserId, CancellationToken cancellationToken) => Task.FromResult(WriteResult);
        public Task<bool> RemoveAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken) => Task.FromResult(WriteResult);
        public Task<IReadOnlyList<ConnectionEdge>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Edges);
        public Task<IReadOnlyList<ConnectionEdge>> GetPendingReceivedAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Edges);
        public Task<IReadOnlyList<ConnectionEdge>> GetPendingSentAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Edges);
        public Task<IReadOnlyList<Guid>> GetMutualConnectionsAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<ConnectionStatusDto> GetStatusAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectionStatusDto(RelationshipStatus.None, 0, 0));
        public Task<IReadOnlyList<SuggestionEdge>> GetSuggestionsAsync(Guid userId, int limit, CancellationToken cancellationToken) => Task.FromResult(Suggestions);
        public Task<IReadOnlyList<NetworkDegreeDto>> GetDegreesAsync(Guid userId, IReadOnlyCollection<Guid> candidateIds, CancellationToken cancellationToken) => Task.FromResult(Degrees);
    }
}
