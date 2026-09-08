using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.CreateRecurringAssignment;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Studies;

public class CreateRecurringAssignmentCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private CreateRecurringAssignmentCommandHandler CreateSut() => new(_db, new FixedClock(Now));

    private async Task<(User user, Subject subject)> SeedAsync(bool activeSubject = true)
    {
        var user = User.Register(500, "nick", "Ada", null, UserRole.Student, Now);
        var subject = Subject.Create("Mathematics", null, Now);
        if (!activeSubject)
        {
            subject.Deactivate();
        }

        _db.Users.Add(user);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();
        return (user, subject);
    }

    private static CreateRecurringAssignmentCommand Command(long telegramUserId, Guid subjectId, int count = 3) => new(
        telegramUserId, subjectId, AssignmentType.Homework, "Weekly homework", null,
        new DateTime(2026, 9, 11, 18, 0, 0, DateTimeKind.Utc), RecurrenceFrequency.Weekly, 1, count);

    [Fact]
    public async Task Materialises_all_occurrences_with_reminders()
    {
        var (user, subject) = await SeedAsync();

        var result = await CreateSut().Handle(Command(user.TelegramUserId, subject.Id, count: 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);

        var assignments = await _db.Assignments.OrderBy(a => a.DueAtUtc).ToListAsync();
        assignments.Should().HaveCount(3);
        assignments.Select(a => a.DueAtUtc).Should().Equal(
            new DateTime(2026, 9, 11, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 18, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 25, 18, 0, 0, DateTimeKind.Utc));

        // Each future occurrence schedules reminders.
        (await _db.Reminders.CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var (_, subject) = await SeedAsync();

        var result = await CreateSut().Handle(Command(999, subject.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }

    [Fact]
    public async Task Fails_when_subject_is_inactive()
    {
        var (user, subject) = await SeedAsync(activeSubject: false);

        var result = await CreateSut().Handle(Command(user.TelegramUserId, subject.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AssignmentErrors.SubjectNotFound);
    }
}
