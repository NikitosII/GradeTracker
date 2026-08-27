using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Domain.Reminders;
using EduTrack.Domain.Studies;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Reminders;

/// <summary>
/// Keeps an assignment's reminders in sync with its due date.
/// </summary>
public static class ReminderPlanner
{
    /// <summary>Cancels the assignment's pending reminders and schedules a fresh set for its due date.</summary>
    public static async Task SyncAsync(IApplicationDbContext db, Assignment assignment, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var pending = await db.Reminders
            .Where(r => r.AssignmentId == assignment.Id && r.Status == ReminderStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var reminder in pending)
        {
            reminder.Cancel(nowUtc);
        }

        Schedule(db, assignment, ReminderKind.Ahead24h, assignment.DueAtUtc.AddHours(-24), nowUtc);
        Schedule(db, assignment, ReminderKind.Ahead2h, assignment.DueAtUtc.AddHours(-2), nowUtc);
        Schedule(db, assignment, ReminderKind.Overdue, assignment.DueAtUtc, nowUtc);
    }

    private static void Schedule(
        IApplicationDbContext db,
        Assignment assignment,
        ReminderKind kind,
        DateTime sendAtUtc,
        DateTime nowUtc)
    {
        if (sendAtUtc <= nowUtc)
        {
            return;
        }

        db.Reminders.Add(Reminder.Create(assignment.OwnerUserId, assignment.Id, kind, sendAtUtc, nowUtc));
    }
}
