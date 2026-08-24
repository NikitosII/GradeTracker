namespace EduTrack.Application.Notifications;

/// <summary>Pure quiet-hours calculation, evaluated in the user's time zone.</summary>
public static class QuietHours
{
    public static bool IsWithin(DateTime utcNow, string? timeZoneId, int startHour, int endHour)
    {
        if (startHour == endHour)
        {
            return false;
        }

        var tz = ResolveTimeZone(timeZoneId);
        var localHour = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz).Hour;

        return startHour < endHour
            ? localHour >= startHour && localHour < endHour
            : localHour >= startHour || localHour < endHour;
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
