using EduTrack.Application.Reminders;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Domain.Reminders;
using EduTrack.Domain.Studies;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Reminders;

public class ReminderPlannerTests
{
    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private static Assignment NewAssignment(DateTime dueAtUtc)
    {
        var owner = Guid.NewGuid();
        return Assignment.Create(owner, Guid.NewGuid(), AssignmentType.Homework, "Essay", null, dueAtUtc, owner, Now);
    }

    [Fact]
    public async Task Schedules_all_three_reminders_for_a_distant_deadline()
    {
        var assignment = NewAssignment(Now.AddDays(3));
        _db.Assignments.Add(assignment);

        await ReminderPlanner.SyncAsync(_db, assignment, Now, CancellationToken.None);
        await _db.SaveChangesAsync();

        var reminders = await _db.Reminders.OrderBy(r => r.SendAtUtc).ToListAsync();
        reminders.Should().HaveCount(3);
        reminders[0].SendAtUtc.Should().Be(assignment.DueAtUtc.AddHours(-24));
        reminders[1].SendAtUtc.Should().Be(assignment.DueAtUtc.AddHours(-2));
        reminders[2].SendAtUtc.Should().Be(assignment.DueAtUtc);
        reminders.Should().OnlyContain(r => r.Status == ReminderStatus.Pending && r.UserId == assignment.OwnerUserId);
    }

    [Fact]
    public async Task Skips_windows_already_in_the_past()
    {
        // Due in one hour: the 24h and 2h windows have already passed; only overdue remains.
        var assignment = NewAssignment(Now.AddHours(1));
        _db.Assignments.Add(assignment);

        await ReminderPlanner.SyncAsync(_db, assignment, Now, CancellationToken.None);
        await _db.SaveChangesAsync();

        var reminders = await _db.Reminders.ToListAsync();
        reminders.Should().ContainSingle();
        reminders[0].Kind.Should().Be(ReminderKind.Overdue);
    }

    [Fact]
    public async Task Resync_cancels_the_previous_pending_reminders()
    {
        var assignment = NewAssignment(Now.AddDays(3));
        _db.Assignments.Add(assignment);
        await ReminderPlanner.SyncAsync(_db, assignment, Now, CancellationToken.None);
        await _db.SaveChangesAsync();

        // The deadline moves; re-sync from the new date.
        assignment.Update(AssignmentType.Homework, "Essay", null, Now.AddDays(5), assignment.OwnerUserId, Now);
        await ReminderPlanner.SyncAsync(_db, assignment, Now, CancellationToken.None);
        await _db.SaveChangesAsync();

        var pending = await _db.Reminders.Where(r => r.Status == ReminderStatus.Pending).ToListAsync();
        var cancelled = await _db.Reminders.Where(r => r.Status == ReminderStatus.Cancelled).ToListAsync();

        pending.Should().HaveCount(3);
        cancelled.Should().HaveCount(3);
        pending.Should().OnlyContain(r => r.SendAtUtc >= Now.AddDays(5).AddHours(-24));
    }
}
