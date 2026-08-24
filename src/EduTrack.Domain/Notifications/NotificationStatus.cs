namespace EduTrack.Domain.Notifications;

public enum NotificationStatus
{
    /// <summary>Delivered to Telegram.</summary>
    Sent = 0,

    /// <summary>Intentionally not sent (notifications disabled or quiet hours).</summary>
    Suppressed = 1,

    /// <summary>Delivery failed.</summary>
    Failed = 2,
}
