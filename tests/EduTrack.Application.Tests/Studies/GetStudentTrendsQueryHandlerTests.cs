using EduTrack.Application.Studies.Queries.GetStudentTrends;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GetStudentTrendsQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private GetStudentTrendsQueryHandler CreateSut() => new(_db, new FixedClock(Now));

    private async Task<User> SeedUserAsync(long telegramId = 500)
    {
        var user = User.Register(telegramId, "nick", "Ada", null, UserRole.Student, Now);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Computes_current_and_previous_window_per_subject()
    {
        var user = await SeedUserAsync();
        var math = Subject.Create("Mathematics", null, Now);
        var physics = Subject.Create("Physics", null, Now);
        _db.Subjects.AddRange(math, physics);

        // Math: current window (−10d) = 5, previous window (−40d) = 3  -> delta +2.
        _db.Grades.Add(Grade.Add(user.Id, math.Id, 5, 1m, null, Now.AddDays(-10), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, math.Id, 3, 1m, null, Now.AddDays(-40), user.Id, Now));
        // Physics: only current window (−5d) = 4, no previous.
        _db.Grades.Add(Grade.Add(user.Id, physics.Id, 4, 1m, null, Now.AddDays(-5), user.Id, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new GetStudentTrendsQuery(user.TelegramUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var subjects = result.Value.Subjects;
        subjects.Should().HaveCount(2);

        var mathTrend = subjects.Single(s => s.SubjectName == "Mathematics");
        mathTrend.CurrentAverage.Should().Be(5.0);
        mathTrend.PreviousAverage.Should().Be(3.0);
        mathTrend.Delta.Should().Be(2.0);

        var physicsTrend = subjects.Single(s => s.SubjectName == "Physics");
        physicsTrend.CurrentAverage.Should().Be(4.0);
        physicsTrend.PreviousAverage.Should().BeNull();
        physicsTrend.Delta.Should().BeNull();
    }

    [Fact]
    public async Task Ignores_grades_older_than_the_previous_window()
    {
        var user = await SeedUserAsync();
        var math = Subject.Create("Mathematics", null, Now);
        _db.Subjects.Add(math);
        _db.Grades.Add(Grade.Add(user.Id, math.Id, 2, 1m, null, Now.AddDays(-90), user.Id, Now)); // too old
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new GetStudentTrendsQuery(user.TelegramUserId), CancellationToken.None);

        result.Value.Subjects.Should().BeEmpty();
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(new GetStudentTrendsQuery(999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }
}
