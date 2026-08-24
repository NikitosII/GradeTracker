namespace EduTrack.Domain.Notifications;

/// <summary>
/// Record of a delivery attempt for a single notification.
/// </summary>
public class NotificationLog
{
    private NotificationLog()
    {
    }

    private NotificationLog(
        Guid id,
        Guid userId,
        NotificationType type,
        NotificationStatus status,
        int? telegramMessageId,
        string? error,
        DateTime nowUtc)
    {
        Id = id;
        UserId = userId;
        Type = type;
        Status = status;
        TelegramMessageId = telegramMessageId;
        Error = error;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationStatus Status { get; private set; }
    public int? TelegramMessageId { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static NotificationLog Sent(Guid id, Guid userId, NotificationType type, int telegramMessageId, DateTime nowUtc)
        => new(id, userId, type, NotificationStatus.Sent, telegramMessageId, null, nowUtc);

    public static NotificationLog Suppressed(Guid id, Guid userId, NotificationType type, string reason, DateTime nowUtc)
        => new(id, userId, type, NotificationStatus.Suppressed, null, reason, nowUtc);

    public static NotificationLog Failed(Guid id, Guid userId, NotificationType type, string error, DateTime nowUtc)
        => new(id, userId, type, NotificationStatus.Failed, null, error, nowUtc);
}
