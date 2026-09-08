using EduTrack.Application.Admin;
using EduTrack.Application.Admin.Commands.SendAnnouncement;
using EduTrack.Application.Localization;
using EduTrack.Application.Notifications;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Admin;

public class SendAnnouncementCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private SendAnnouncementCommandHandler CreateSut() => new(_db, new FixedClock(Now), new ResxTranslator());

    [Fact]
    public async Task Queues_one_outbox_message_per_user_and_audits()
    {
        _db.Users.Add(User.Register(100, "root", "Root", null, UserRole.Admin, Now));
        _db.Users.Add(User.Register(200, "bob", "Bob", null, UserRole.Student, Now));
        _db.Users.Add(User.Register(300, "cara", "Cara", null, UserRole.Student, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new SendAnnouncementCommand(100, "Maintenance tonight"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);

        var outbox = await _db.OutboxMessages.ToListAsync();
        outbox.Should().HaveCount(3);
        outbox.Should().OnlyContain(m => m.Type == OutboxWriter.UserNotificationType && m.ProcessedAtUtc == null);

        var audit = await _db.AuditLogs.SingleAsync();
        audit.Action.Should().Be(AuditActions.AnnouncementSent);
    }

    [Fact]
    public async Task Fails_when_caller_is_not_admin()
    {
        _db.Users.Add(User.Register(200, "bob", "Bob", null, UserRole.Student, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new SendAnnouncementCommand(200, "hi"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErrors.NotAdmin);
        (await _db.OutboxMessages.AnyAsync()).Should().BeFalse();
    }
}
