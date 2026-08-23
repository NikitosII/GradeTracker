using EduTrack.Application.Admin;
using EduTrack.Application.Admin.Commands.CreateInviteCode;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Admin;

public class CreateInviteCodeCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private CreateInviteCodeCommandHandler CreateSut() => new(_db, new FixedClock(Now));

    private async Task<User> SeedAdminAsync()
    {
        var admin = User.Register(100, "root", "Root", null, UserRole.Admin, Now);
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task Creates_code_and_writes_audit_for_admin()
    {
        var admin = await SeedAdminAsync();

        var result = await CreateSut().Handle(
            new CreateInviteCodeCommand(admin.TelegramUserId, UserRole.Student, ExpiresInDays: 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(nameof(UserRole.Student));
        result.Value.Code.Should().NotBeNullOrWhiteSpace();

        var stored = await _db.InviteCodes.SingleAsync();
        stored.Role.Should().Be(UserRole.Student);
        stored.ExpiresAt.Should().Be(Now.AddDays(7));

        var audit = await _db.AuditLogs.SingleAsync();
        audit.Action.Should().Be(AuditActions.InviteCodeCreated);
        audit.UserId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task Fails_when_caller_is_not_admin()
    {
        _db.Users.Add(User.Register(200, "bob", "Bob", null, UserRole.Student, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new CreateInviteCodeCommand(200, UserRole.Student, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErrors.NotAdmin);
        (await _db.InviteCodes.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Fails_when_caller_not_bound()
    {
        var result = await CreateSut().Handle(
            new CreateInviteCodeCommand(999, UserRole.Admin, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }
}
