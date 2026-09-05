namespace EduTrack.Application.Users;

/// <summary>The user-configurable preferences surfaced by the /settings menu.</summary>
public sealed record UserSettingsDto(
    string TimeZone,
    string Language,
    bool NotificationsEnabled,
    bool MorningDigestEnabled,
    bool Reminder24hEnabled,
    bool Reminder2hEnabled,
    int? QuietHoursStart,
    int? QuietHoursEnd);
