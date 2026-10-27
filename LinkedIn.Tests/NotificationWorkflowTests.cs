using System.Text.Json;
using BuildingBlocks.Events;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application;
using NotificationService.Domain;
using NotificationService.Infrastructure;

namespace LinkedIn.Tests;

/// <summary>Who gets notified for each event, duplicate protection, and the read/unread API rules.</summary>
public sealed class NotificationWorkflowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ----- Event -> notification mapping -----

    [Fact]
    public void Requirement_created_notifies_every_matched_professional()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var @event = new RequirementCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Boundary wall", "MASON", "Gurgaon",
            [new MatchingProfessional(first, 95), new MatchingProfessional(second, 72)], DateTime.UtcNow);

        var commands = NotificationEventMapper.Map(EventTopics.RequirementCreated, JsonSerializer.Serialize(@event, Json));

        Assert.Equal([first, second], commands.Select(c => c.RecipientUserId));
        Assert.All(commands, c => Assert.Equal(@event.EventId, c.EventId));
        Assert.All(commands, c => Assert.Equal("NewRequirementMatch", c.Type));
    }

    [Fact]
    public void Interest_submitted_notifies_the_customer()
    {
        var @event = new ProfessionalInterestSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Boundary wall", "Rajesh Kumar", DateTime.UtcNow);

        var command = Assert.Single(NotificationEventMapper.Map(EventTopics.ProfessionalInterestSubmitted, JsonSerializer.Serialize(@event, Json)));

        Assert.Equal(@event.CustomerId, command.RecipientUserId);
        Assert.Contains("Rajesh Kumar", command.Message);
    }

    [Theory]
    [InlineData("ACCEPTED", "InterestAccepted")]
    [InlineData("REJECTED", "InterestRejected")]
    public void Decision_notifies_the_professional_with_matching_type(string decision, string expectedType)
    {
        var @event = new ProfessionalInterestDecisionEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Boundary wall", "Priya", decision, DateTime.UtcNow);

        var command = Assert.Single(NotificationEventMapper.Map(EventTopics.ProfessionalInterestDecision, JsonSerializer.Serialize(@event, Json)));

        Assert.Equal(@event.ProfessionalId, command.RecipientUserId);
        Assert.Equal(expectedType, command.Type);
    }

    [Fact]
    public void Cancellation_notifies_affected_professionals()
    {
        var affected = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var @event = new RequirementCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Boundary wall", "Priya", affected, DateTime.UtcNow);

        var commands = NotificationEventMapper.Map(EventTopics.RequirementCancelled, JsonSerializer.Serialize(@event, Json));

        Assert.Equal(affected, commands.Select(c => c.RecipientUserId));
    }

    [Fact]
    public void Generated_messages_never_exceed_column_length()
    {
        var @event = new ProfessionalInterestSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('x', 600), "Rajesh", DateTime.UtcNow);

        var command = Assert.Single(NotificationEventMapper.Map(EventTopics.ProfessionalInterestSubmitted, JsonSerializer.Serialize(@event, Json)));

        Assert.True(command.Message.Length <= 500);
        Assert.True(new CreateNotificationValidator().Validate(command).IsValid);
    }

    [Fact]
    public void Unknown_topic_produces_no_notifications() =>
        Assert.Empty(NotificationEventMapper.Map("something-else", "{}"));

    [Fact]
    public void Unreadable_payload_is_a_permanent_failure()
    {
        var exception = Assert.ThrowsAny<JsonException>(() => NotificationEventMapper.Map(EventTopics.ProfessionalInterestSubmitted, "not json"));

        Assert.True(KafkaNotificationConsumer.IsPermanent(exception));
        Assert.True(KafkaNotificationConsumer.IsPermanent(new ValidationException("bad")));
        Assert.False(KafkaNotificationConsumer.IsPermanent(new TimeoutException("database unavailable")));
    }

    // ----- Handlers -----

    [Fact]
    public async Task Same_event_delivered_twice_creates_one_notification()
    {
        var repository = new InMemoryNotificationRepository();
        var handler = new CreateNotificationCommandHandler(repository, NullLogger<CreateNotificationCommandHandler>.Instance);
        var command = new CreateNotificationCommand(Guid.NewGuid(), "ProfessionalInterested", "Title", "Message", Guid.NewGuid(), "Requirement", Guid.NewGuid());

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Single(repository.Items);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task User_cannot_mark_someone_elses_notification_as_read()
    {
        var repository = new InMemoryNotificationRepository();
        var owner = Guid.NewGuid();
        var notification = repository.Seed(owner);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new MarkNotificationReadCommandHandler(repository)
            .Handle(new MarkNotificationReadCommand(notification.Id, Guid.NewGuid()), CancellationToken.None));
        Assert.False(notification.IsRead);

        await new MarkNotificationReadCommandHandler(repository).Handle(new MarkNotificationReadCommand(notification.Id, owner), CancellationToken.None);
        Assert.True(notification.IsRead);
    }

    [Fact]
    public async Task Page_reports_total_and_unread_counts_for_the_recipient_only()
    {
        var repository = new InMemoryNotificationRepository();
        var me = Guid.NewGuid();
        repository.Seed(me);
        repository.Seed(me).IsRead = true;
        repository.Seed(me);
        repository.Seed(Guid.NewGuid());

        var page = await new GetNotificationsQueryHandler(repository).Handle(new GetNotificationsQuery(me, 1, 2), CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.UnreadCount);
        Assert.All(page.Items, item => Assert.Equal(me, item.RecipientUserId));
    }

    [Fact]
    public void Page_size_is_limited()
    {
        Assert.False(new GetNotificationsValidator().Validate(new GetNotificationsQuery(Guid.NewGuid(), 1, 500)).IsValid);
        Assert.False(new GetNotificationsValidator().Validate(new GetNotificationsQuery(Guid.NewGuid(), 0, 20)).IsValid);
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        public List<Notification> Items { get; } = [];

        public Notification Seed(Guid recipient)
        {
            var notification = new Notification { Id = Guid.NewGuid(), RecipientUserId = recipient, Type = "T", Title = "T", Message = "M", CreatedAtUtc = DateTime.UtcNow.AddSeconds(Items.Count) };
            Items.Add(notification);
            return notification;
        }

        public Task AddAsync(Notification notification, CancellationToken cancellationToken) { Items.Add(notification); return Task.CompletedTask; }
        public Task<Notification?> GetByEventAsync(Guid eventId, Guid recipientUserId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(n => n.EventId == eventId && n.RecipientUserId == recipientUserId));
        public Task<IReadOnlyList<Notification>> GetPageForRecipientAsync(Guid recipientUserId, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Notification>>(Items.Where(n => n.RecipientUserId == recipientUserId).OrderByDescending(n => n.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToList());
        public Task<int> CountForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken) => Task.FromResult(Items.Count(n => n.RecipientUserId == recipientUserId));
        public Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken) => Task.FromResult(Items.Count(n => n.RecipientUserId == recipientUserId && !n.IsRead));
        public Task<bool> MarkReadAsync(Guid id, Guid recipientUserId, CancellationToken cancellationToken)
        {
            var notification = Items.FirstOrDefault(n => n.Id == id && n.RecipientUserId == recipientUserId);
            if (notification is null) return Task.FromResult(false);
            notification.IsRead = true;
            return Task.FromResult(true);
        }
        public Task<int> MarkAllReadAsync(Guid recipientUserId, CancellationToken cancellationToken)
        {
            var unread = Items.Where(n => n.RecipientUserId == recipientUserId && !n.IsRead).ToList();
            unread.ForEach(n => n.IsRead = true);
            return Task.FromResult(unread.Count);
        }
    }
}
