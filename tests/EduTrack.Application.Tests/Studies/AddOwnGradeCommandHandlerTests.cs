using EduTrack.Application.Admin;
using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.AddOwnGrade;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Studies;

public class AddOwnGradeCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private AddOwnGradeCommandHandler CreateSut() => new(_db, _clock);

    private async Task<(User user, Subject subject)> SeedUserAndSubjectAsync(bool subjectActive = true)
    {
        var user = User.Register(100, "nick", "Ada", "Lovelace", UserRole.Student, Now);
        var subject = Subject.Create("Mathematics", null, Now);
        if (!subjectActive)
        {
            subject.Deactivate();
        }

        _db.Users.Add(user);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();
        return (user, subject);
    }

    [Fact]
    public async Task Adds_grade_for_bound_user_and_active_subject()
    {
        var (user, subject) = await SeedUserAndSubjectAsync();

        var result = await CreateSut().Handle(
            new AddOwnGradeCommand(100, subject.Id, 5, 1.5m, "board answer", Now), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SubjectName.Should().Be("Mathematics");
        result.Value.Value.Should().Be(5);

        var grade = await _db.Grades.SingleAsync();
        grade.StudentUserId.Should().Be(user.Id);
        grade.CreatedByUserId.Should().Be(user.Id);
        grade.UpdatedByUserId.Should().Be(user.Id);
        grade.Weight.Should().Be(1.5m);

        var audit = await _db.AuditLogs.SingleAsync();
        audit.Action.Should().Be(AuditActions.GradeAdded);
        audit.UserId.Should().Be(user.Id);
        audit.NewValue.Should().Be("Mathematics: 5");
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var subject = Subject.Create("Physics", null, Now);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new AddOwnGradeCommand(999, subject.Id, 4, 1m, null, Now), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
        (await _db.Grades.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Fails_when_subject_missing_or_inactive()
    {
        var (_, subject) = await SeedUserAndSubjectAsync(subjectActive: false);

        var result = await CreateSut().Handle(
            new AddOwnGradeCommand(100, subject.Id, 4, 1m, null, Now), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GradeErrors.SubjectNotFound);
    }
}
