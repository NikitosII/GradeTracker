using EduTrack.Application.Admin;
using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.SetOwnGradeArchived;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Studies;

public class SetOwnGradeArchivedCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private SetOwnGradeArchivedCommandHandler CreateSut() => new(_db, _clock);

    private async Task<(User user, Grade grade)> SeedAsync(bool archived = false)
    {
        var user = User.Register(100, "nick", "Ada", "Lovelace", UserRole.Student, Now);
        var subject = Subject.Create("Physics", null, Now);
        _db.Users.Add(user);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var grade = Grade.Add(user.Id, subject.Id, 5, 1m, "good", Now.AddDays(-1), user.Id, Now);
        if (archived)
        {
            grade.Archive(user.Id, Now);
        }

        _db.Grades.Add(grade);
        await _db.SaveChangesAsync();
        return (user, grade);
    }

    [Fact]
    public async Task Archives_own_grade_and_writes_audit()
    {
        var (user, grade) = await SeedAsync();

        var result = await CreateSut().Handle(
            new SetOwnGradeArchivedCommand(100, grade.Id, Archived: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var stored = await _db.Grades.IgnoreQueryFilters().SingleAsync(g => g.Id == grade.Id);
        stored.IsArchived.Should().BeTrue();

        var audit = await _db.AuditLogs.SingleAsync();
        audit.Action.Should().Be(AuditActions.GradeArchived);
        audit.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Restores_archived_grade()
    {
        var (_, grade) = await SeedAsync(archived: true);

        var result = await CreateSut().Handle(
            new SetOwnGradeArchivedCommand(100, grade.Id, Archived: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Grades.IgnoreQueryFilters().SingleAsync(g => g.Id == grade.Id)).IsArchived.Should().BeFalse();
        (await _db.AuditLogs.SingleAsync()).Action.Should().Be(AuditActions.GradeUnarchived);
    }

    [Fact]
    public async Task Fails_when_not_owner()
    {
        var (_, grade) = await SeedAsync();
        var other = User.Register(200, "bob", "Bob", "B", UserRole.Student, Now);
        _db.Users.Add(other);
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new SetOwnGradeArchivedCommand(200, grade.Id, Archived: true), CancellationToken.None);

        result.Error.Should().Be(GradeErrors.NotOwner);
    }

    [Fact]
    public async Task Fails_when_grade_missing()
    {
        await SeedAsync();

        var result = await CreateSut().Handle(
            new SetOwnGradeArchivedCommand(100, Guid.NewGuid(), Archived: true), CancellationToken.None);

        result.Error.Should().Be(GradeErrors.NotFound);
    }
}
