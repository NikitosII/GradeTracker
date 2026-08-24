namespace EduTrack.Application.Notifications;

/// <summary>Notification behaviour configuration (spec §25).</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Hour (0-23) at which quiet hours begin.</summary>
    public int QuietHoursStart { get; set; } = 22;

    /// <summary>Hour (0-23) at which quiet hours end.</summary>
    public int QuietHoursEnd { get; set; } = 8;

    /// <summary>Hour (0-23, user local) at which the morning digest is sent.</summary>
    public int MorningDigestHour { get; set; } = 8;
}
