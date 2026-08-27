using EduTrack.Domain.Common;

namespace EduTrack.Application.Reminders;

public static class ReminderErrors
{
    public static readonly Error NotFound = Error.NotFound("Reminders.NotFound", "Reminder not found.");

    public static readonly Error NotOwner = Error.AccessDenied("Reminders.NotOwner", "You can only snooze your own reminders.");
}
