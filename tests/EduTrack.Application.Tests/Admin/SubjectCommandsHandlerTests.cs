using EduTrack.Application.Admin;
using EduTrack.Application.Admin.Commands.CreateSubject;
using EduTrack.Application.Admin.Commands.UpdateSubject;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Admin;

public class SubjectCommandsHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private async Task<User> SeedAdminAsync()
    {
        var admin = User.Register(100, "root", "Root", null, UserRole.Admin, Now);
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task Create_adds_subject_for_admin()
    {
        var admin = await SeedAdminAsync();

        var result = await new CreateSubjectCommandHandler(_db, _clock).Handle(
            new CreateSubjectCommand(admin.TelegramUserId, "Chemistry", "Reactions"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Chemistry");

        var stored = await _db.Subjects.SingleAsync();
        stored.Description.Should().Be("Reactions");
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_fails_when_name_taken()
    {
        var admin = await SeedAdminAsync();
        _db.Subjects.Add(Subject.Create("Physics", null, Now));
        await _db.SaveChangesAsync();

        var result = await new CreateSubjectCommandHandler(_db, _clock).Handle(
            new CreateSubjectCommand(admin.TelegramUserId, "Physics", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErrors.SubjectNameTaken);
    }

    [Fact]
    public async Task Update_toggles_active_and_preserves_description()
    {
        var admin = await SeedAdminAsync();
        var subject = Subject.Create("Physics", "Mechanics", Now);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var result = await new UpdateSubjectCommandHandler(_db, _clock).Handle(
            new UpdateSubjectCommand(admin.TelegramUserId, subject.Id, "Physics", "Mechanics", IsActive: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();

        var stored = await _db.Subjects.SingleAsync();
        stored.IsActive.Should().BeFalse();
        stored.Description.Should().Be("Mechanics");
    }

    [Fact]
    public async Task Create_fails_when_caller_not_admin()
    {
        _db.Users.Add(User.Register(200, "bob", "Bob", null, UserRole.Student, Now));
        await _db.SaveChangesAsync();

        var result = await new CreateSubjectCommandHandler(_db, _clock).Handle(
            new CreateSubjectCommand(200, "History", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErrors.NotAdmin);
    }
}
