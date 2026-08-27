namespace EduTrack.Domain.Reminders;

/// <summary>
/// A scheduled reminder for a student's deadline.
/// </summary>
public class Reminder
{
    public const int MaxLastErrorLength = 1024;

    private Reminder()
    {
    }

    private Reminder(
        Guid id,
        Guid userId,
        Guid assignmentId,
        ReminderKind kind,
        DateTime sendAtUtc,
        DateTime nowUtc)
    {
        Id = id;
        UserId = userId;
        AssignmentId = assignmentId;
        Kind = kind;
        SendAtUtc = sendAtUtc;
        Status = ReminderStatus.Pending;
        CreatedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>The student the reminder is delivered to.</summary>
    public Guid UserId { get; private set; }

    /// <summary>The deadline this reminder belongs to.</summary>
    public Guid AssignmentId { get; private set; }

    public ReminderKind Kind { get; private set; }
    public DateTime SendAtUtc { get; private set; }
    public ReminderStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Reminder Create(
        Guid userId,
        Guid assignmentId,
        ReminderKind kind,
        DateTime sendAtUtc,
        DateTime nowUtc)
        => new(Guid.NewGuid(), userId, assignmentId, kind, sendAtUtc, nowUtc);

    /// <summary>Marks the reminder handed off to the notification pipeline.</summary>
    public void MarkSent(DateTime nowUtc)
    {
        Status = ReminderStatus.Sent;
        LastError = null;
        UpdatedAt = nowUtc;
    }

    /// <summary>Records a failed hand-off; the scheduler may retry it.</summary>
    public void MarkFailed(string error, DateTime nowUtc)
    {
        Status = ReminderStatus.Failed;
        RetryCount++;
        LastError = Truncate(error);
        UpdatedAt = nowUtc;
    }

    /// <summary>Cancels a pending reminder .</summary>
    public void Cancel(DateTime nowUtc)
    {
        Status = ReminderStatus.Cancelled;
        UpdatedAt = nowUtc;
    }

    /// <summary>Moves the reminder to a new time and returns it to the pending queue.</summary>
    public void Reschedule(DateTime sendAtUtc, DateTime nowUtc)
    {
        SendAtUtc = sendAtUtc;
        Status = ReminderStatus.Pending;
        LastError = null;
        UpdatedAt = nowUtc;
    }

    private static string Truncate(string value)
        => value.Length <= MaxLastErrorLength ? value : value[..MaxLastErrorLength];
}
