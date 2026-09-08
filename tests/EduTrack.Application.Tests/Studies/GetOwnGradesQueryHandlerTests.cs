using EduTrack.Application.Studies.Queries.GetOwnGrades;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GetOwnGradesQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private GetOwnGradesQueryHandler CreateSut() => new(_db);

    [Fact]
    public async Task Returns_paged_grades_with_average_over_full_set()
    {
        var user = User.Register(100, "nick", "Ada", null, UserRole.Student, Now);
        var subject = Subject.Create("Mathematics", null, Now);
        _db.Users.Add(user);
        _db.Subjects.Add(subject);

        // Values 5,4,5,3 -> average 4.25 (matches the spec example).
        foreach (var (value, day) in new[] { (5, 1), (4, 2), (5, 3), (3, 4) })
        {
            _db.Grades.Add(Grade.Add(user.Id, subject.Id, value, 1m, null, Now.AddDays(day), user.Id, Now));
        }

        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new GetOwnGradesQuery(100, subject.Id, Page: 1, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var page = result.Value;
        page.TotalCount.Should().Be(4);
        page.Items.Should().HaveCount(2);
        page.Average.Should().Be(4.25);
        page.TotalPages.Should().Be(2);
        page.HasNext.Should().BeTrue();
        page.HasPrevious.Should().BeFalse();
        // Ordered by OccurredAt descending -> newest (day 4, value 3) first.
        page.Items[0].Value.Should().Be(3);
    }

    [Fact]
    public async Task Average_is_weighted_by_grade_weight()
    {
        var user = User.Register(101, "bob", "Bob", null, UserRole.Student, Now);
        var subject = Subject.Create("Physics", null, Now);
        _db.Users.Add(user);
        _db.Subjects.Add(subject);

        // (5*3 + 3*1) / (3+1) = 18/4 = 4.5  (unweighted mean would be 4.0).
        _db.Grades.Add(Grade.Add(user.Id, subject.Id, 5, 3m, null, Now.AddDays(1), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, subject.Id, 3, 1m, null, Now.AddDays(2), user.Id, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new GetOwnGradesQuery(101, subject.Id), CancellationToken.None);

        result.Value.Average.Should().Be(4.5);
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(
            new GetOwnGradesQuery(999, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }
}
