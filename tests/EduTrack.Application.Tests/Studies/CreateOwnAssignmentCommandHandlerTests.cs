using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.CreateOwnAssignment;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Studies;

public class CreateOwnAssignmentCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Due = new(2026, 8, 25, 18, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private CreateOwnAssignmentCommandHandler CreateSut() => new(_db, _clock);

    private async Task<(User user, Subject subject)> SeedUserAndSubjectAsync(bool subjectActive = true)
    {
        var user = User.Register(100, "nick", "Ada", "Lovelace", UserRole.Student, Now);
        var subject = Subject.Create("Physics", null, Now);
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
    public async Task Creates_deadline_for_bound_user_and_active_subject()
    {
        var (user, subject) = await SeedUserAndSubjectAsync();

        var result = await CreateSut().Handle(
            new CreateOwnAssignmentCommand(100, subject.Id, AssignmentType.Lab, "Lab #3", "measurements", Due),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SubjectName.Should().Be("Physics");
        result.Value.Type.Should().Be(AssignmentType.Lab);
        result.Value.Title.Should().Be("Lab #3");
        result.Value.DueAtUtc.Should().Be(Due);

        var assignment = await _db.Assignments.SingleAsync();
        assignment.OwnerUserId.Should().Be(user.Id);
        assignment.CreatedByUserId.Should().Be(user.Id);
        assignment.UpdatedByUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var subject = Subject.Create("Physics", null, Now);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new CreateOwnAssignmentCommand(999, subject.Id, AssignmentType.Homework, "hw", null, Due),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
        (await _db.Assignments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Fails_when_subject_missing_or_inactive()
    {
        var (_, subject) = await SeedUserAndSubjectAsync(subjectActive: false);

        var result = await CreateSut().Handle(
            new CreateOwnAssignmentCommand(100, subject.Id, AssignmentType.Homework, "hw", null, Due),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AssignmentErrors.SubjectNotFound);
    }
}
