using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Users.Commands.UpdateSettings;

public sealed record UpdateUserSettingsCommand(
    long TelegramUserId,
    string TimeZone,
    string Language,
    bool NotificationsEnabled,
    bool MorningDigestEnabled,
    bool Reminder24hEnabled,
    bool Reminder2hEnabled,
    int? QuietHoursStart,
    int? QuietHoursEnd) : ICommand<UserSettingsDto>;
