using EduTrack.Application.Studies.Queries.GetStudentRecommendations;
using EduTrack.Application.Studies.Recommendations;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GetStudentRecommendationsQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private GetStudentRecommendationsQueryHandler CreateSut() => new(_db, _clock);

    private async Task<User> SeedUserAsync()
    {
        var user = User.Register(100, "nick", "Ada", "Lovelace", UserRole.Student, Now);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Subject> SeedSubjectAsync(string name)
    {
        var subject = Subject.Create(name, null, Now);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();
        return subject;
    }

    private IReadOnlyList<RecommendationDto> Items(StudentRecommendationsDto dto) => dto.Items;

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(new GetStudentRecommendationsQuery(999), CancellationToken.None);
        result.Error.Should().Be(UserErrors.NotBound);
    }

    [Fact]
    public async Task Suggests_onboarding_when_no_grades()
    {
        await SeedUserAsync();

        var result = await CreateSut().Handle(new GetStudentRecommendationsQuery(100), CancellationToken.None);

        Items(result.Value).Should().ContainSingle()
            .Which.Kind.Should().Be(RecommendationKind.NoGradesYet);
    }

    [Fact]
    public async Task Flags_overdue_and_urgent_deadlines()
    {
        var user = await SeedUserAsync();
        var subject = await SeedSubjectAsync("Physics");
        _db.Assignments.Add(Assignment.Create(user.Id, subject.Id, AssignmentType.Homework, "Late essay", null, Now.AddDays(-1), user.Id, Now));
        _db.Assignments.Add(Assignment.Create(user.Id, subject.Id, AssignmentType.Exam, "Midterm", null, Now.AddDays(3), user.Id, Now));
        await _db.SaveChangesAsync();

        var items = Items((await CreateSut().Handle(new GetStudentRecommendationsQuery(100), CancellationToken.None)).Value);

        items.Should().Contain(r => r.Kind == RecommendationKind.OverdueDeadlines && r.Count == 1);
        var urgent = items.Single(r => r.Kind == RecommendationKind.UrgentDeadline);
        urgent.Title.Should().Be("Midterm");
        // Overdue must be ranked above the urgent tip.
        items.ToList().FindIndex(r => r.Kind == RecommendationKind.OverdueDeadlines)
            .Should().BeLessThan(items.ToList().FindIndex(r => r.Kind == RecommendationKind.UrgentDeadline));
    }

    [Fact]
    public async Task Flags_week_workload_when_two_or_more_due_this_week()
    {
        var user = await SeedUserAsync();
        var subject = await SeedSubjectAsync("Physics");
        _db.Assignments.Add(Assignment.Create(user.Id, subject.Id, AssignmentType.Homework, "HW1", null, Now.AddDays(1), user.Id, Now));
        _db.Assignments.Add(Assignment.Create(user.Id, subject.Id, AssignmentType.Homework, "HW2", null, Now.AddDays(4), user.Id, Now));
        await _db.SaveChangesAsync();

        var items = Items((await CreateSut().Handle(new GetStudentRecommendationsQuery(100), CancellationToken.None)).Value);

        items.Should().Contain(r => r.Kind == RecommendationKind.WeekWorkload && r.Count == 2);
    }

    [Fact]
    public async Task Flags_falling_average()
    {
        var user = await SeedUserAsync();
        var subject = await SeedSubjectAsync("Physics");
        // Previous 30d window (−60..−30): high; current 30d: low.
        _db.Grades.Add(Grade.Add(user.Id, subject.Id, 5, 1m, null, Now.AddDays(-45), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, subject.Id, 3, 1m, null, Now.AddDays(-10), user.Id, Now));
        await _db.SaveChangesAsync();

        var items = Items((await CreateSut().Handle(new GetStudentRecommendationsQuery(100), CancellationToken.None)).Value);

        var falling = items.Single(r => r.Kind == RecommendationKind.FallingAverage);
        falling.SubjectName.Should().Be("Physics");
        falling.PreviousValue.Should().Be(5);
        falling.Value.Should().Be(3);
    }

    [Fact]
    public async Task Flags_low_subject_all_time()
    {
        var user = await SeedUserAsync();
        var subject = await SeedSubjectAsync("Chemistry");
        // Old grades (outside the trend window) so only the all-time low rule fires.
        _db.Grades.Add(Grade.Add(user.Id, subject.Id, 2, 1m, null, Now.AddDays(-200), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, subject.Id, 3, 1m, null, Now.AddDays(-190), user.Id, Now));
        await _db.SaveChangesAsync();

        var items = Items((await CreateSut().Handle(new GetStudentRecommendationsQuery(100), CancellationToken.None)).Value);

        var low = items.Single(r => r.Kind == RecommendationKind.LowSubject);
        low.SubjectName.Should().Be("Chemistry");
        low.Value.Should().Be(2.5);
    }

    [Fact]
    public async Task Caps_at_five_recommendations()
    {
        var user = await SeedUserAsync();
        var physics = await SeedSubjectAsync("Physics");
        var chem = await SeedSubjectAsync("Chemistry");
        var math = await SeedSubjectAsync("Math");

        // Overdue + several upcoming this week (urgent + workload).
        _db.Assignments.Add(Assignment.Create(user.Id, physics.Id, AssignmentType.Homework, "Late", null, Now.AddDays(-1), user.Id, Now));
        _db.Assignments.Add(Assignment.Create(user.Id, physics.Id, AssignmentType.Homework, "Soon1", null, Now.AddDays(1), user.Id, Now));
        _db.Assignments.Add(Assignment.Create(user.Id, physics.Id, AssignmentType.Homework, "Soon2", null, Now.AddDays(2), user.Id, Now));

        // Two falling subjects + one low subject.
        _db.Grades.Add(Grade.Add(user.Id, physics.Id, 5, 1m, null, Now.AddDays(-45), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, physics.Id, 2, 1m, null, Now.AddDays(-5), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, chem.Id, 5, 1m, null, Now.AddDays(-50), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, chem.Id, 3, 1m, null, Now.AddDays(-3), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, math.Id, 2, 1m, null, Now.AddDays(-300), user.Id, Now));
        await _db.SaveChangesAsync();

        var items = Items((await CreateSut().Handle(new GetStudentRecommendationsQuery(100), CancellationToken.None)).Value);

        items.Count.Should().Be(5);
        items[0].Kind.Should().Be(RecommendationKind.OverdueDeadlines);
    }
}
