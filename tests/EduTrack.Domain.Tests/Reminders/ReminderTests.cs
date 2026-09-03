using EduTrack.Domain.Reminders;
using FluentAssertions;

namespace EduTrack.Domain.Tests.Reminders;

public class ReminderTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Assignment = Guid.NewGuid();

    [Fact]
    public void Create_starts_pending()
    {
        var sendAt = Now.AddHours(5);

        var reminder = Reminder.Create(User, Assignment, ReminderKind.Ahead24h, sendAt, Now);

        reminder.UserId.Should().Be(User);
        reminder.AssignmentId.Should().Be(Assignment);
        reminder.Kind.Should().Be(ReminderKind.Ahead24h);
        reminder.SendAtUtc.Should().Be(sendAt);
        reminder.Status.Should().Be(ReminderStatus.Pending);
        reminder.RetryCount.Should().Be(0);
        reminder.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkSent_moves_to_sent()
    {
        var reminder = Reminder.Create(User, Assignment, ReminderKind.Ahead2h, Now.AddHours(1), Now);

        reminder.MarkSent(Now.AddHours(1));

        reminder.Status.Should().Be(ReminderStatus.Sent);
    }

    [Fact]
    public void MarkFailed_records_error_and_increments_retry()
    {
        var reminder = Reminder.Create(User, Assignment, ReminderKind.Overdue, Now, Now);

        reminder.MarkFailed("broker down", Now.AddMinutes(1));

        reminder.Status.Should().Be(ReminderStatus.Failed);
        reminder.RetryCount.Should().Be(1);
        reminder.LastError.Should().Be("broker down");
    }

    [Fact]
    public void Cancel_moves_to_cancelled()
    {
        var reminder = Reminder.Create(User, Assignment, ReminderKind.Ahead24h, Now.AddHours(3), Now);

        reminder.Cancel(Now.AddMinutes(5));

        reminder.Status.Should().Be(ReminderStatus.Cancelled);
    }

    [Fact]
    public void Reschedule_returns_a_sent_reminder_to_pending_at_the_new_time()
    {
        var reminder = Reminder.Create(User, Assignment, ReminderKind.Ahead2h, Now.AddHours(1), Now);
        reminder.MarkSent(Now.AddHours(1));

        var newTime = Now.AddHours(4);
        reminder.Reschedule(newTime, Now.AddHours(1));

        reminder.Status.Should().Be(ReminderStatus.Pending);
        reminder.SendAtUtc.Should().Be(newTime);
        reminder.LastError.Should().BeNull();
    }
}
