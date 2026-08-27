namespace EduTrack.Domain.Reminders;

public enum ReminderKind
{
    /// <summary>Fires 24 hours before the deadline.</summary>
    Ahead24h = 0,

    /// <summary>Fires 2 hours before the deadline.</summary>
    Ahead2h = 1,

    /// <summary>Fires when the deadline passes.</summary>
    Overdue = 2,
}
