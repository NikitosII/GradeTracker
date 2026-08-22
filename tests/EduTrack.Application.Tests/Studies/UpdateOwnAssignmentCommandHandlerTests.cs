using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.UpdateOwnAssignment;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Studies;

public class UpdateOwnAssignmentCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Due = new(2026, 8, 25, 18, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private UpdateOwnAssignmentCommandHandler CreateSut() => new(_db, _clock);

    private async Task<(User owner, Subject subject, Assignment assignment)> SeedAssignmentAsync()
    {
        var owner = User.Register(100, "nick", "Ada", null, UserRole.Student, Now);
        var subject = Subject.Create("Physics", null, Now);
        var assignment = Assignment.Create(owner.Id, subject.Id, AssignmentType.Homework, "old title", "old", Due, owner.Id, Now);

        _db.Users.Add(owner);
        _db.Subjects.Add(subject);
        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync();
        return (owner, subject, assignment);
    }

    [Fact]
    public async Task Updates_own_deadline()
    {
        var (owner, _, assignment) = await SeedAssignmentAsync();
        var later = Now.AddHours(3);
        var newDue = Due.AddDays(1);

        var result = await new UpdateOwnAssignmentCommandHandler(_db, new FixedClock(later)).Handle(
            new UpdateOwnAssignmentCommand(100, assignment.Id, AssignmentType.Exam, "new title", "new", newDue),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(AssignmentType.Exam);
        result.Value.Title.Should().Be("new title");

        var stored = await _db.Assignments.SingleAsync();
        stored.Type.Should().Be(AssignmentType.Exam);
        stored.Title.Should().Be("new title");
        stored.DueAtUtc.Should().Be(newDue);
        stored.UpdatedByUserId.Should().Be(owner.Id);
        stored.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public async Task Fails_when_deadline_missing()
    {
        await SeedAssignmentAsync();

        var result = await CreateSut().Handle(
            new UpdateOwnAssignmentCommand(100, Guid.NewGuid(), AssignmentType.Test, "x", null, Due),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AssignmentErrors.NotFound);
    }

    [Fact]
    public async Task Fails_when_deadline_belongs_to_another_student()
    {
        var (_, _, assignment) = await SeedAssignmentAsync();
        _db.Users.Add(User.Register(200, "bob", "Bob", null, UserRole.Student, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new UpdateOwnAssignmentCommand(200, assignment.Id, AssignmentType.Test, "x", null, Due),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AssignmentErrors.NotOwner);
    }
}
