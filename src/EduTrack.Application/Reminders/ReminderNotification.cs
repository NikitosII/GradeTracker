using EduTrack.Domain.Notifications;
using EduTrack.Domain.Reminders;

namespace EduTrack.Application.Reminders;

/// <summary>Turns a due reminder into the notification the pipeline delivers.</summary>
public static class ReminderNotification
{
    /// <summary>The message content and notification type for a reminder of the given kind.</summary>
    public static (NotificationType Type, string Title, string Body) Build(
        ReminderKind kind,
        string subjectName,
        string assignmentTitle,
        DateTime dueAtUtc,
        string? timeZoneId)
    {
        var localDue = ToLocal(dueAtUtc, timeZoneId);
        var due = localDue.ToString("ddd d MMM, HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        return kind switch
        {
            ReminderKind.Ahead24h => (
                NotificationType.AssignmentReminder24h,
                "Deadline in 24 hours",
                $"{subjectName} - {assignmentTitle}\nDue {due}."),

            ReminderKind.Ahead2h => (
                NotificationType.AssignmentReminder2h,
                "Deadline in 2 hours",
                $"{subjectName} - {assignmentTitle}\nDue {due}."),

            ReminderKind.Overdue => (
                NotificationType.AssignmentOverdue,
                "Deadline passed",
                $"{subjectName} - {assignmentTitle}\nWas due {due}."),

            _ => (
                NotificationType.AssignmentReminder24h,
                "Deadline reminder",
                $"{subjectName} - {assignmentTitle}\nDue {due}."),
        };
    }

    private static DateTime ToLocal(DateTime utc, string? timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
