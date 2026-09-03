using EduTrack.Application.Notifications;
using EduTrack.Application.Reminders;
using EduTrack.Application.Reminders.Commands.SnoozeReminder;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Reminders;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduTrack.Application.Tests.Reminders;

public class SnoozeReminderCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private SnoozeReminderCommandHandler CreateSut() =>
        new(_db, new FixedClock(Now), Options.Create(new NotificationOptions { MorningDigestHour = 8 }));

    private async Task<(User user, Reminder reminder)> SeedAsync()
    {
        var user = User.Register(700, "sam", "Sam", null, UserRole.Student, Now);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var reminder = Reminder.Create(user.Id, Guid.NewGuid(), ReminderKind.Ahead2h, Now.AddHours(-1), Now);
        reminder.MarkSent(Now.AddHours(-1));
        _db.Reminders.Add(reminder);
        await _db.SaveChangesAsync();
        return (user, reminder);
    }

    [Fact]
    public async Task Snooze_one_hour_reschedules_to_pending()
    {
        var (user, reminder) = await SeedAsync();

        var result = await CreateSut().Handle(
            new SnoozeReminderCommand(user.TelegramUserId, reminder.Id, SnoozeOption.OneHour), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Now.AddHours(1));

        var stored = await _db.Reminders.SingleAsync();
        stored.Status.Should().Be(ReminderStatus.Pending);
        stored.SendAtUtc.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public async Task Snooze_tomorrow_morning_uses_the_digest_hour()
    {
        var (user, reminder) = await SeedAsync();

        var result = await CreateSut().Handle(
            new SnoozeReminderCommand(user.TelegramUserId, reminder.Id, SnoozeOption.TomorrowMorning), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // User is UTC; tomorrow at the 08:00 digest hour.
        result.Value.Should().Be(new DateTime(2026, 8, 24, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var (_, reminder) = await SeedAsync();

        var result = await CreateSut().Handle(
            new SnoozeReminderCommand(999, reminder.Id, SnoozeOption.OneHour), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }

    [Fact]
    public async Task Fails_when_reminder_missing()
    {
        var (user, _) = await SeedAsync();

        var result = await CreateSut().Handle(
            new SnoozeReminderCommand(user.TelegramUserId, Guid.NewGuid(), SnoozeOption.OneHour), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReminderErrors.NotFound);
    }

    [Fact]
    public async Task Fails_when_reminder_belongs_to_another_user()
    {
        var (_, reminder) = await SeedAsync();
        var other = User.Register(800, "bob", "Bob", null, UserRole.Student, Now);
        _db.Users.Add(other);
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new SnoozeReminderCommand(other.TelegramUserId, reminder.Id, SnoozeOption.OneHour), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReminderErrors.NotOwner);
    }
}
