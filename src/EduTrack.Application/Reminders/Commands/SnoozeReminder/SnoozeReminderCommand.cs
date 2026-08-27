using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Reminders.Commands.SnoozeReminder;

/// <summary>Reschedules one of the caller's reminders to fire again later.</summary>
public sealed record SnoozeReminderCommand(
    long TelegramUserId,
    Guid ReminderId,
    SnoozeOption Option) : ICommand<DateTime>;
