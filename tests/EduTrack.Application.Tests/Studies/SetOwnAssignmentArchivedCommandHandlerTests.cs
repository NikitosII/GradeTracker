using EduTrack.Application.Admin;
using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.SetOwnAssignmentArchived;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Studies;

public class SetOwnAssignmentArchivedCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private SetOwnAssignmentArchivedCommandHandler CreateSut() => new(_db, _clock);

    private async Task<(User user, Assignment assignment)> SeedAsync(bool archived = false)
    {
        var user = User.Register(100, "nick", "Ada", "Lovelace", UserRole.Student, Now);
        var subject = Subject.Create("Physics", null, Now);
        _db.Users.Add(user);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var assignment = Assignment.Create(user.Id, subject.Id, AssignmentType.Lab, "Lab #3", null, Now.AddDays(2), user.Id, Now);
        if (archived)
        {
            assignment.Archive(user.Id, Now);
        }

        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync();
        return (user, assignment);
    }

    [Fact]
    public async Task Archives_own_deadline_and_writes_audit()
    {
        var (user, assignment) = await SeedAsync();

        var result = await CreateSut().Handle(
            new SetOwnAssignmentArchivedCommand(100, assignment.Id, Archived: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var stored = await _db.Assignments.IgnoreQueryFilters().SingleAsync(a => a.Id == assignment.Id);
        stored.IsArchived.Should().BeTrue();
        stored.ArchivedAt.Should().Be(Now);

        var audit = await _db.AuditLogs.SingleAsync();
        audit.Action.Should().Be(AuditActions.DeadlineArchived);
        audit.UserId.Should().Be(user.Id);
        audit.NewValue.Should().Contain("Lab #3");
    }

    [Fact]
    public async Task Restores_archived_deadline()
    {
        var (_, assignment) = await SeedAsync(archived: true);

        var result = await CreateSut().Handle(
            new SetOwnAssignmentArchivedCommand(100, assignment.Id, Archived: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var stored = await _db.Assignments.IgnoreQueryFilters().SingleAsync(a => a.Id == assignment.Id);
        stored.IsArchived.Should().BeFalse();
        (await _db.AuditLogs.SingleAsync()).Action.Should().Be(AuditActions.DeadlineUnarchived);
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var (_, assignment) = await SeedAsync();

        var result = await CreateSut().Handle(
            new SetOwnAssignmentArchivedCommand(999, assignment.Id, Archived: true), CancellationToken.None);

        result.Error.Should().Be(UserErrors.NotBound);
    }

    [Fact]
    public async Task Fails_when_not_owner()
    {
        var (_, assignment) = await SeedAsync();
        var other = User.Register(200, "bob", "Bob", "B", UserRole.Student, Now);
        _db.Users.Add(other);
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new SetOwnAssignmentArchivedCommand(200, assignment.Id, Archived: true), CancellationToken.None);

        result.Error.Should().Be(AssignmentErrors.NotOwner);
    }

    [Fact]
    public async Task Fails_when_deadline_missing()
    {
        await SeedAsync();

        var result = await CreateSut().Handle(
            new SetOwnAssignmentArchivedCommand(100, Guid.NewGuid(), Archived: true), CancellationToken.None);

        result.Error.Should().Be(AssignmentErrors.NotFound);
    }
}
