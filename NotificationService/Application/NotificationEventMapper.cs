using System.Text.Json;
using BuildingBlocks.Events;

namespace NotificationService.Application;

/// <summary>
/// Turns an integration event from another service into the notifications it should produce.
/// The source service has already applied its business rules; this class only decides who is told what.
/// Pure function (no I/O) so it can be unit tested without Kafka or a database.
/// </summary>
public static class NotificationEventMapper
{
    public const string RequirementReference = "Requirement";
    /// <summary>ReferenceId of connection notifications is the other user, so the UI can open their profile.</summary>
    public const string UserReference = "User";
    public const string PostReference = "Post";
    private const int MaxMessageLength = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Topics this service subscribes to.</summary>
    public static readonly IReadOnlyList<string> Topics =
    [
        EventTopics.RequirementCreated,
        EventTopics.RequirementCancelled,
        EventTopics.ProfessionalInterestSubmitted,
        EventTopics.ProfessionalInterestDecision,
        EventTopics.ConnectionRequested,
        EventTopics.ConnectionAccepted,
        EventTopics.PostLiked,
        EventTopics.PostCommented
    ];

    /// <summary>
    /// Returns one command per recipient. Throws <see cref="JsonException"/> when the payload cannot be read;
    /// such a message can never succeed and is sent to the dead-letter topic by the consumer.
    /// </summary>
    public static IReadOnlyList<CreateNotificationCommand> Map(string topic, string payload) => topic switch
    {
        EventTopics.RequirementCreated => FromRequirementCreated(Read<RequirementCreatedEvent>(payload)),
        EventTopics.RequirementCancelled => FromRequirementCancelled(Read<RequirementCancelledEvent>(payload)),
        EventTopics.ProfessionalInterestSubmitted => [FromInterestSubmitted(Read<ProfessionalInterestSubmittedEvent>(payload))],
        EventTopics.ProfessionalInterestDecision => [FromInterestDecision(Read<ProfessionalInterestDecisionEvent>(payload))],
        EventTopics.ConnectionRequested => [FromConnectionRequested(Read<ConnectionRequestedEvent>(payload))],
        EventTopics.ConnectionAccepted => [FromConnectionAccepted(Read<ConnectionAcceptedEvent>(payload))],
        EventTopics.PostLiked => [FromPostLiked(Read<PostLikedEvent>(payload))],
        EventTopics.PostCommented => [FromPostCommented(Read<PostCommentedEvent>(payload))],
        _ => []
    };

    private static IReadOnlyList<CreateNotificationCommand> FromRequirementCreated(RequirementCreatedEvent message) =>
        message.MatchingProfessionals.Select(professional => new CreateNotificationCommand(
            professional.ProfessionalId,
            "NewRequirementMatch",
            "New Requirement Matching Your Profile",
            Truncate($"A new requirement matching your {message.ProfessionalType} profile has been posted in {message.Location}. Match score: {professional.MatchScore}%."),
            message.RequirementId,
            RequirementReference,
            message.EventId)).ToList();

    private static IReadOnlyList<CreateNotificationCommand> FromRequirementCancelled(RequirementCancelledEvent message) =>
        message.AffectedProfessionalIds.Select(professionalId => new CreateNotificationCommand(
            professionalId,
            "RequirementCancelled",
            "Requirement Cancelled",
            Truncate($"{message.CustomerName} cancelled the requirement \"{message.RequirementTitle}\"."),
            message.RequirementId,
            RequirementReference,
            message.EventId)).ToList();

    private static CreateNotificationCommand FromInterestSubmitted(ProfessionalInterestSubmittedEvent message) => new(
        message.CustomerId,
        "ProfessionalInterested",
        "New Professional Interest",
        Truncate($"{message.ProfessionalName} is interested in your requirement \"{message.RequirementTitle}\"."),
        message.RequirementId,
        RequirementReference,
        message.EventId);

    private static CreateNotificationCommand FromInterestDecision(ProfessionalInterestDecisionEvent message)
    {
        var accepted = string.Equals(message.Decision, "ACCEPTED", StringComparison.OrdinalIgnoreCase);
        return new CreateNotificationCommand(
            message.ProfessionalId,
            accepted ? "InterestAccepted" : "InterestRejected",
            accepted ? "You Have Been Selected" : "Not Selected",
            Truncate(accepted
                ? $"{message.CustomerName} selected you for \"{message.RequirementTitle}\"."
                : $"{message.CustomerName} did not select you for \"{message.RequirementTitle}\"."),
            message.RequirementId,
            RequirementReference,
            message.EventId);
    }

    private static CreateNotificationCommand FromConnectionRequested(ConnectionRequestedEvent message) => new(
        message.AddresseeUserId,
        "ConnectionRequested",
        "New Connection Request",
        Truncate($"{message.RequesterName} wants to connect with you."),
        message.RequesterUserId,
        UserReference,
        message.EventId);

    private static CreateNotificationCommand FromConnectionAccepted(ConnectionAcceptedEvent message) => new(
        message.RequesterUserId,
        "ConnectionAccepted",
        "Connection Accepted",
        Truncate($"{message.AddresseeName} accepted your connection request."),
        message.AddresseeUserId,
        UserReference,
        message.EventId);

    private static CreateNotificationCommand FromPostLiked(PostLikedEvent message) => new(
        message.PostAuthorUserId,
        "PostLiked",
        "Your Post Was Liked",
        Truncate($"{message.LikerName} liked your post \"{message.PostExcerpt}\"."),
        message.PostId,
        PostReference,
        message.EventId);

    private static CreateNotificationCommand FromPostCommented(PostCommentedEvent message) => new(
        message.PostAuthorUserId,
        "PostCommented",
        "New Comment On Your Post",
        Truncate($"{message.CommenterName} commented: \"{message.CommentExcerpt}\""),
        message.PostId,
        PostReference,
        message.EventId);

    private static T Read<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload, JsonOptions) ?? throw new JsonException($"{typeof(T).Name} payload was empty.");

    /// <summary>Keeps generated text within the 500-character Message column so a long title cannot block the consumer.</summary>
    private static string Truncate(string text) => text.Length <= MaxMessageLength ? text : text[..(MaxMessageLength - 3)] + "...";
}
