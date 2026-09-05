using EduTrack.Application.Admin;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Commands.UpdateSettings;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Users;

public class UpdateUserSettingsCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private UpdateUserSettingsCommandHandler CreateSut() => new(_db, new FixedClock(Now));

    private async Task<User> SeedUserAsync()
    {
        var user = User.Register(500, "nick", "Ada", null, UserRole.Student, Now);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private static UpdateUserSettingsCommand Command(long telegramUserId) => new(
        telegramUserId,
        TimeZone: "Europe/Moscow",
        Language: "en",
        NotificationsEnabled: true,
        MorningDigestEnabled: false,
        Reminder24hEnabled: true,
        Reminder2hEnabled: false,
        QuietHoursStart: 23,
        QuietHoursEnd: 7);

    [Fact]
    public async Task Persists_settings_and_writes_audit()
    {
        var user = await SeedUserAsync();

        var result = await CreateSut().Handle(Command(user.TelegramUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TimeZone.Should().Be("Europe/Moscow");
        result.Value.MorningDigestEnabled.Should().BeFalse();
        result.Value.QuietHoursStart.Should().Be(23);

        var stored = await _db.Users.SingleAsync(u => u.Id == user.Id);
        stored.Language.Should().Be("en");
        stored.Reminder2hEnabled.Should().BeFalse();
        stored.QuietHoursEnd.Should().Be(7);

        var audit = await _db.AuditLogs.SingleAsync();
        audit.Action.Should().Be(AuditActions.SettingsUpdated);
        audit.NewValue.Should().Contain("tz=Europe/Moscow");
    }

    [Fact]
    public async Task No_op_change_writes_no_audit()
    {
        var user = await SeedUserAsync();
        var unchanged = new UpdateUserSettingsCommand(
            user.TelegramUserId,
            user.TimeZone,
            user.Language,
            NotificationsEnabled: true,
            MorningDigestEnabled: true,
            Reminder24hEnabled: true,
            Reminder2hEnabled: true,
            QuietHoursStart: null,
            QuietHoursEnd: null);

        var result = await CreateSut().Handle(unchanged, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.AuditLogs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(Command(999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }
}
