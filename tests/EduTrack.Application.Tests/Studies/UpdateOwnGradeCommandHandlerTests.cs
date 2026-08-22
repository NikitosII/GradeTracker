using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.UpdateOwnGrade;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Studies;

public class UpdateOwnGradeCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private UpdateOwnGradeCommandHandler CreateSut() => new(_db, _clock);

    private async Task<(User owner, Subject subject, Grade grade)> SeedGradeAsync()
    {
        var owner = User.Register(100, "nick", "Ada", null, UserRole.Student, Now);
        var subject = Subject.Create("Mathematics", null, Now);
        var grade = Grade.Add(owner.Id, subject.Id, 3, 1m, "old", Now, owner.Id, Now);

        _db.Users.Add(owner);
        _db.Subjects.Add(subject);
        _db.Grades.Add(grade);
        await _db.SaveChangesAsync();
        return (owner, subject, grade);
    }

    [Fact]
    public async Task Updates_own_grade()
    {
        var (owner, _, grade) = await SeedGradeAsync();
        var later = Now.AddHours(3);

        var result = await new UpdateOwnGradeCommandHandler(_db, new FixedClock(later)).Handle(
            new UpdateOwnGradeCommand(100, grade.Id, 5, 2m, "fixed", later), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(5);

        var stored = await _db.Grades.SingleAsync();
        stored.Value.Should().Be(5);
        stored.Weight.Should().Be(2m);
        stored.UpdatedByUserId.Should().Be(owner.Id);
        stored.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public async Task Fails_when_grade_missing()
    {
        await SeedGradeAsync();

        var result = await CreateSut().Handle(
            new UpdateOwnGradeCommand(100, Guid.NewGuid(), 5, 1m, null, Now), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GradeErrors.NotFound);
    }

    [Fact]
    public async Task Fails_when_grade_belongs_to_another_student()
    {
        var (_, subject, grade) = await SeedGradeAsync();
        _db.Users.Add(User.Register(200, "bob", "Bob", null, UserRole.Student, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new UpdateOwnGradeCommand(200, grade.Id, 5, 1m, null, Now), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GradeErrors.NotOwner);
    }
}
