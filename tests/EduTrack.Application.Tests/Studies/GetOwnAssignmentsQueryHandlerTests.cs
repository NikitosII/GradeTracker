using EduTrack.Application.Studies.Queries.GetOwnAssignments;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GetOwnAssignmentsQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private GetOwnAssignmentsQueryHandler CreateSut() => new(_db);

    private async Task<(User user, Subject subject)> SeedAsync()
    {
        var user = User.Register(100, "nick", "Ada", null, UserRole.Student, Now);
        var subject = Subject.Create("Physics", null, Now);
        _db.Users.Add(user);
        _db.Subjects.Add(subject);

        // One past, three future (due in 2h, 3d, 10d).
        _db.Assignments.Add(Assignment.Create(user.Id, subject.Id, AssignmentType.Homework, "past", null, Now.AddDays(-1), user.Id, Now));
        _db.Assignments.Add(Assignment.Create(user.Id, subject.Id, AssignmentType.Test, "soon", null, Now.AddHours(2), user.Id, Now));
        _db.Assignments.Add(Assignment.Create(user.Id, subject.Id, AssignmentType.Exam, "midweek", null, Now.AddDays(3), user.Id, Now));
        _db.Assignments.Add(Assignment.Create(user.Id, subject.Id, AssignmentType.Project, "later", null, Now.AddDays(10), user.Id, Now));

        await _db.SaveChangesAsync();
        return (user, subject);
    }

    [Fact]
    public async Task Returns_upcoming_ordered_by_due_ascending()
    {
        await SeedAsync();

        var result = await CreateSut().Handle(
            new GetOwnAssignmentsQuery(100, null, Now, null, Page: 1, PageSize: 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var page = result.Value;
        page.TotalCount.Should().Be(3); // the past one is excluded by FromUtc = Now
        page.Items.Select(a => a.Title).Should().ContainInOrder("soon", "midweek", "later");
    }

    [Fact]
    public async Task Filters_to_a_window_for_week_view()
    {
        await SeedAsync();

        var result = await CreateSut().Handle(
            new GetOwnAssignmentsQuery(100, null, Now, Now.AddDays(7), Page: 1, PageSize: 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(a => a.Title).Should().ContainInOrder("soon", "midweek");
        result.Value.Items.Should().HaveCount(2); // "later" (10d) falls outside the window
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(
            new GetOwnAssignmentsQuery(999, null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }
}
