using System.Globalization;

namespace EduTrack.Application.Reminders;

/// <summary>
/// Callback-data contract for the inline snooze buttons attached to reminder notifications.
/// </summary>
public static class ReminderCallback
{
    /// <summary>Namespace prefix used to route reminder callbacks.</summary>
    public const string Namespace = "rmd";

    private const string SnoozeAction = "snz";

    public static string Snooze(Guid reminderId, SnoozeOption option)
        => $"{Namespace}:{SnoozeAction}:{reminderId}:{(int)option}";

    /// <summary>Parses a snooze callback payload.</summary>
    public static bool TryParseSnooze(string data, out Guid reminderId, out SnoozeOption option)
    {
        reminderId = Guid.Empty;
        option = SnoozeOption.OneHour;

        var parts = data.Split(':');
        if (parts.Length != 4 || parts[0] != Namespace || parts[1] != SnoozeAction)
        {
            return false;
        }

        if (!Guid.TryParse(parts[2], out reminderId))
        {
            return false;
        }

        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw)
            || !Enum.IsDefined(typeof(SnoozeOption), raw))
        {
            return false;
        }

        option = (SnoozeOption)raw;
        return true;
    }
}
