namespace BuildingBlocks.Events;

/// <summary>
/// Kafka topic names shared by producers and consumers. Keep in sync with docker-compose.dev.yml (kafka-init).
/// </summary>
public static class EventTopics
{
    public const string RequirementCreated = "requirement-created";
    public const string RequirementCancelled = "requirement-cancelled";
    public const string ProfessionalInterestSubmitted = "professional-interest-submitted";
    public const string ProfessionalInterestDecision = "professional-interest-decision";
    public const string ConnectionRequested = "connection-requested";
    public const string ConnectionAccepted = "connection-accepted";
    public const string PostLiked = "post-liked";
    public const string PostCommented = "post-commented";
    public const string NotificationDeadLetter = "notification-events-dlq";
}
