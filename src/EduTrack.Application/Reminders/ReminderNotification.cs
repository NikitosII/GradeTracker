using System.Globalization;
using EduTrack.Application.Localization;
using EduTrack.Domain.Notifications;
using EduTrack.Domain.Reminders;

namespace EduTrack.Application.Reminders;

/// <summary>Turns a due reminder into the notification the pipeline delivers.</summary>
public static class ReminderNotification
{
    /// <summary>The message content and notification type for a reminder of the given kind, in the recipient's language.</summary>
    public static (NotificationType Type, string Title, string Body) Build(
        ITranslator translator,
        ReminderKind kind,
        string subjectName,
        string assignmentTitle,
        DateTime dueAtUtc,
        string? timeZoneId,
        string? language)
    {
        var culture = ResolveCulture(language);
        var localDue = ToLocal(dueAtUtc, timeZoneId);
        var due = localDue.ToString("ddd d MMM, HH:mm", culture);

        string T(string key, params object[] args) => translator.Find(language, key, args) ?? key;

        return kind switch
        {
            ReminderKind.Ahead24h => (
                NotificationType.AssignmentReminder24h,
                T(TextKeys.NotifyReminder24hTitle),
                T(TextKeys.NotifyReminderDueBody, subjectName, assignmentTitle, due)),

            ReminderKind.Ahead2h => (
                NotificationType.AssignmentReminder2h,
                T(TextKeys.NotifyReminder2hTitle),
                T(TextKeys.NotifyReminderDueBody, subjectName, assignmentTitle, due)),

            ReminderKind.Overdue => (
                NotificationType.AssignmentOverdue,
                T(TextKeys.NotifyReminderOverdueTitle),
                T(TextKeys.NotifyReminderOverdueBody, subjectName, assignmentTitle, due)),

            _ => (
                NotificationType.AssignmentReminder24h,
                T(TextKeys.NotifyReminderTitle),
                T(TextKeys.NotifyReminderDueBody, subjectName, assignmentTitle, due)),
        };
    }

    private static CultureInfo ResolveCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.InvariantCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
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
