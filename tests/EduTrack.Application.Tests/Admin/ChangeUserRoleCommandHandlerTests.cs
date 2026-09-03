using EduTrack.Application.Admin;
using EduTrack.Application.Admin.Commands.ChangeUserRole;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Admin;

public class ChangeUserRoleCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private ChangeUserRoleCommandHandler CreateSut() => new(_db, new FixedClock(Now));

    private async Task<(User admin, User student)> SeedAsync()
    {
        var admin = User.Register(100, "root", "Root", null, UserRole.Admin, Now);
        var student = User.Register(200, "bob", "Bob", null, UserRole.Student, Now);
        _db.Users.AddRange(admin, student);
        await _db.SaveChangesAsync();
        return (admin, student);
    }

    [Fact]
    public async Task Promotes_student_to_admin_and_writes_audit()
    {
        var (admin, student) = await SeedAsync();

        var result = await CreateSut().Handle(
            new ChangeUserRoleCommand(admin.TelegramUserId, student.Id, UserRole.Admin), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(nameof(UserRole.Admin));

        var stored = await _db.Users.SingleAsync(u => u.Id == student.Id);
        stored.Role.Should().Be(UserRole.Admin);

        var audit = await _db.AuditLogs.SingleAsync();
        audit.Action.Should().Be(AuditActions.UserRoleChanged);
        audit.OldValue.Should().Be(nameof(UserRole.Student));
        audit.NewValue.Should().Be(nameof(UserRole.Admin));

        // The affected user is notified via the outbox.
        var outbox = await _db.OutboxMessages.SingleAsync();
        outbox.ProcessedAtUtc.Should().BeNull();
        outbox.Payload.Should().Contain(student.Id.ToString());
    }

    [Fact]
    public async Task Fails_when_admin_targets_self()
    {
        var (admin, _) = await SeedAsync();

        var result = await CreateSut().Handle(
            new ChangeUserRoleCommand(admin.TelegramUserId, admin.Id, UserRole.Student), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErrors.CannotDemoteSelf);
    }

    [Fact]
    public async Task Fails_when_target_missing()
    {
        var (admin, _) = await SeedAsync();

        var result = await CreateSut().Handle(
            new ChangeUserRoleCommand(admin.TelegramUserId, Guid.NewGuid(), UserRole.Admin), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErrors.UserNotFound);
    }

    [Fact]
    public async Task Fails_when_caller_is_not_admin()
    {
        var (_, student) = await SeedAsync();

        var result = await CreateSut().Handle(
            new ChangeUserRoleCommand(student.TelegramUserId, student.Id, UserRole.Admin), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErrors.NotAdmin);
    }
}
