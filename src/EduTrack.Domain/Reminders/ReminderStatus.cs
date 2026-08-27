namespace EduTrack.Domain.Reminders;

/// <summary>Lifecycle of a scheduled reminder.</summary>
public enum ReminderStatus
{
    Pending = 0,
    Sent = 1,
    Cancelled = 2,
    Failed = 3,
}
