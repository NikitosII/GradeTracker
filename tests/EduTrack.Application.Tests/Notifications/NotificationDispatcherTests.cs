using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Notifications;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Domain.Notifications;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace EduTrack.Application.Tests.Notifications;

public class NotificationDispatcherTests
{
    private static readonly DateTime Noon = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Night = new(2026, 8, 23, 23, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly ITelegramSender _telegram = Substitute.For<ITelegramSender>();
    private readonly IApplicationMetrics _metrics = Substitute.For<IApplicationMetrics>();

    private NotificationDispatcher CreateSut(DateTime now) =>
        new(_db, _telegram, new FixedClock(now), _metrics,
            Options.Create(new NotificationOptions { QuietHoursStart = 22, QuietHoursEnd = 8 }));

    private async Task<User> SeedUserAsync(bool notificationsEnabled = true, Action<User>? configure = null)
    {
        var user = User.Register(500, "nick", "Ada", null, UserRole.Student, Noon);
        if (!notificationsEnabled)
        {
            user.SetNotificationsEnabled(false, Noon);
        }

        configure?.Invoke(user);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private UserNotificationRequested Message(Guid userId, bool important, NotificationType type = NotificationType.SystemAnnouncement) =>
        new(Guid.NewGuid(), userId, type, "Title", "Body", important);

    [Fact]
    public async Task Sends_and_logs_for_enabled_user()
    {
        var user = await SeedUserAsync();
        _telegram.SendNotificationAsync(user.TelegramUserId, Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>()).Returns(4242);

        var message = Message(user.Id, important: false);
        await CreateSut(Noon).DispatchAsync(message, CancellationToken.None);

        await _telegram.Received(1).SendNotificationAsync(user.TelegramUserId, Arg.Is<string>(s => s.Contains("Body")), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>());
        var log = await _db.NotificationLogs.SingleAsync();
        log.Status.Should().Be(NotificationStatus.Sent);
        log.TelegramMessageId.Should().Be(4242);
        _metrics.Received(1).NotificationSent(NotificationType.SystemAnnouncement);
    }

    [Fact]
    public async Task Suppresses_when_notifications_disabled()
    {
        var user = await SeedUserAsync(notificationsEnabled: false);

        await CreateSut(Noon).DispatchAsync(Message(user.Id, important: false), CancellationToken.None);

        await _telegram.DidNotReceive().SendNotificationAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>());
        (await _db.NotificationLogs.SingleAsync()).Status.Should().Be(NotificationStatus.Suppressed);
    }

    [Fact]
    public async Task Suppresses_during_quiet_hours()
    {
        var user = await SeedUserAsync();

        await CreateSut(Night).DispatchAsync(Message(user.Id, important: false), CancellationToken.None);

        await _telegram.DidNotReceive().SendNotificationAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>());
        (await _db.NotificationLogs.SingleAsync()).Status.Should().Be(NotificationStatus.Suppressed);
    }

    [Fact]
    public async Task Suppresses_a_reminder_type_the_user_turned_off()
    {
        var user = await SeedUserAsync(configure: u => u.SetReminder24hEnabled(false, Noon));

        await CreateSut(Noon).DispatchAsync(
            Message(user.Id, important: false, NotificationType.AssignmentReminder24h), CancellationToken.None);

        await _telegram.DidNotReceive().SendNotificationAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>());
        (await _db.NotificationLogs.SingleAsync()).Status.Should().Be(NotificationStatus.Suppressed);
    }

    [Fact]
    public async Task Uses_per_user_quiet_hours_over_the_global_default()
    {
        // Global quiet hours are 22-8, so noon would normally send; the user's
        // custom 8-18 window covers noon and must suppress it.
        var user = await SeedUserAsync(configure: u => u.SetQuietHours(8, 18, Noon));

        await CreateSut(Noon).DispatchAsync(Message(user.Id, important: false), CancellationToken.None);

        await _telegram.DidNotReceive().SendNotificationAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>());
        (await _db.NotificationLogs.SingleAsync()).Status.Should().Be(NotificationStatus.Suppressed);
    }

    [Fact]
    public async Task Important_message_bypasses_settings_and_quiet_hours()
    {
        var user = await SeedUserAsync(notificationsEnabled: false);
        _telegram.SendNotificationAsync(user.TelegramUserId, Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>()).Returns(7);

        await CreateSut(Night).DispatchAsync(
            Message(user.Id, important: true, NotificationType.AdminDataChange), CancellationToken.None);

        await _telegram.Received(1).SendNotificationAsync(user.TelegramUserId, Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>());
        (await _db.NotificationLogs.SingleAsync()).Status.Should().Be(NotificationStatus.Sent);
    }

    [Fact]
    public async Task Is_idempotent_for_a_known_notification_id()
    {
        var user = await SeedUserAsync();
        _db.NotificationLogs.Add(NotificationLog.Sent(
            Guid.NewGuid(), user.Id, NotificationType.SystemAnnouncement, 1, Noon));
        await _db.SaveChangesAsync();
        var existing = await _db.NotificationLogs.SingleAsync();

        var message = new UserNotificationRequested(existing.Id, user.Id, NotificationType.SystemAnnouncement, "T", "B", false);
        await CreateSut(Noon).DispatchAsync(message, CancellationToken.None);

        await _telegram.DidNotReceive().SendNotificationAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logs_failed_when_recipient_missing()
    {
        await CreateSut(Noon).DispatchAsync(Message(Guid.NewGuid(), important: true), CancellationToken.None);

        await _telegram.DidNotReceive().SendNotificationAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<IReadOnlyList<InlineButton>>?>(), Arg.Any<CancellationToken>());
        (await _db.NotificationLogs.SingleAsync()).Status.Should().Be(NotificationStatus.Failed);
        _metrics.Received(1).NotificationFailed(Arg.Any<NotificationType>());
    }
}
