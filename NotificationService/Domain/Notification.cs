namespace NotificationService.Domain;

public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
