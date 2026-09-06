using System.Text;
using EduTrack.Application.Studies.Queries.ExportCalendar;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class ExportCalendarQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private ExportCalendarQueryHandler CreateSut() => new(_db, new FixedClock(Now));

    private async Task<User> SeedUserAsync(long telegramId = 500)
    {
        var user = User.Register(telegramId, "nick", "Ada", null, UserRole.Student, Now);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Exports_the_owner_deadlines_as_ics()
    {
        var user = await SeedUserAsync();
        var subject = Subject.Create("Mathematics", null, Now);
        _db.Subjects.Add(subject);
        _db.Assignments.Add(Assignment.Create(
            user.Id, subject.Id, AssignmentType.Exam, "Calculus final", "Bring a calculator",
            new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc), user.Id, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new ExportCalendarQuery(user.TelegramUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EventCount.Should().Be(1);
        result.Value.FileName.Should().EndWith(".ics");

        var ics = Encoding.UTF8.GetString(result.Value.Content);
        ics.Should().Contain("SUMMARY:[Exam] Calculus final");
        ics.Should().Contain("DTSTART:20260920T090000Z");
        ics.Should().Contain("@edutrack"); // stable per-assignment UID
        ics.Should().Contain("Mathematics");
    }

    [Fact]
    public async Task Excludes_other_users_deadlines()
    {
        var owner = await SeedUserAsync(500);
        var other = User.Register(600, "bob", "Bob", null, UserRole.Student, Now);
        _db.Users.Add(other);
        var subject = Subject.Create("Physics", null, Now);
        _db.Subjects.Add(subject);
        _db.Assignments.Add(Assignment.Create(
            other.Id, subject.Id, AssignmentType.Lab, "Not mine", null,
            new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc), other.Id, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new ExportCalendarQuery(owner.TelegramUserId), CancellationToken.None);

        result.Value.EventCount.Should().Be(0);
        Encoding.UTF8.GetString(result.Value.Content).Should().NotContain("Not mine");
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(new ExportCalendarQuery(999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }
}
