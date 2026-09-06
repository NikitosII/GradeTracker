using EduTrack.Application.Studies.Queries.GetStudentStats;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GetStudentStatsQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private GetStudentStatsQueryHandler CreateSut() => new(_db, new FixedClock(Now));

    private async Task<User> SeedUserAsync(long telegramId = 500)
    {
        var user = User.Register(telegramId, "nick", "Ada", null, UserRole.Student, Now);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Aggregates_averages_subjects_and_deadlines()
    {
        var user = await SeedUserAsync();
        var math = Subject.Create("Mathematics", null, Now);
        var physics = Subject.Create("Physics", null, Now);
        _db.Subjects.AddRange(math, physics);

        // Math: 5 this week, 4 outside the month.
        _db.Grades.Add(Grade.Add(user.Id, math.Id, 5, 1m, null, Now.AddDays(-2), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, math.Id, 4, 1m, null, Now.AddDays(-40), user.Id, Now));
        // Physics: 3 this week.
        _db.Grades.Add(Grade.Add(user.Id, physics.Id, 3, 1m, null, Now.AddDays(-1), user.Id, Now));

        _db.Assignments.Add(Assignment.Create(user.Id, math.Id, AssignmentType.Exam, "Upcoming", null, Now.AddDays(5), user.Id, Now));
        _db.Assignments.Add(Assignment.Create(user.Id, math.Id, AssignmentType.Quiz, "Past", null, Now.AddDays(-5), user.Id, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new GetStudentStatsQuery(user.TelegramUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var s = result.Value;

        s.OverallGpa.Should().Be(4.0);        // (5+4+3)/3
        s.WeekCount.Should().Be(2);
        s.WeekAverage.Should().Be(4.0);       // (5+3)/2
        s.MonthCount.Should().Be(2);          // the -40d grade is excluded
        s.TotalGrades.Should().Be(3);
        s.UpcomingDeadlines.Should().Be(1);   // only the future assignment

        s.Subjects.Should().HaveCount(2);
        s.Subjects[0].SubjectName.Should().Be("Mathematics"); // highest average first
        s.Subjects[0].Average.Should().Be(4.5);               // (5+4)/2
        s.WorstSubject.Should().Be("Physics");
        s.WorstSubjectAverage.Should().Be(3.0);
    }

    [Fact]
    public async Task No_grades_returns_empty_snapshot()
    {
        var user = await SeedUserAsync();

        var result = await CreateSut().Handle(new GetStudentStatsQuery(user.TelegramUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalGrades.Should().Be(0);
        result.Value.OverallGpa.Should().BeNull();
        result.Value.Subjects.Should().BeEmpty();
        result.Value.WorstSubject.Should().BeNull();
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(new GetStudentStatsQuery(999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }
}
