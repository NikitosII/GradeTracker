using EduTrack.Application.Reminders.Digests;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Reminders;

public class MorningDigestComposerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 6, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private MorningDigestComposer CreateSut() => new(_db);

    private async Task<User> SeedUserAsync() // UTC user
    {
        var user = User.Register(100, "nick", "Ada", null, UserRole.Student, Now);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Composes_today_deadlines_and_averages()
    {
        var user = await SeedUserAsync();
        var physics = Subject.Create("Physics", null, Now);
        _db.Subjects.Add(physics);
        await _db.SaveChangesAsync();

        // Deadline due later today (UTC user, same local day).
        _db.Assignments.Add(Assignment.Create(
            user.Id, physics.Id, AssignmentType.Lab, "Lab #3", null,
            new DateTime(2026, 8, 23, 18, 0, 0, DateTimeKind.Utc), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, physics.Id, 5, 1m, null, Now.AddDays(-2), user.Id, Now));
        _db.Grades.Add(Grade.Add(user.Id, physics.Id, 4, 1m, null, Now.AddDays(-1), user.Id, Now));
        await _db.SaveChangesAsync();

        var body = await CreateSut().ComposeAsync(user, Now, CancellationToken.None);

        body.Should().NotBeNull();
        body!.Should().Contain("Lab #3");
        body.Should().Contain("Physics");
        body.Should().Contain("Average score");
        body.Should().Contain("Physics: 4.5");
    }

    [Fact]
    public async Task Returns_null_when_there_is_nothing_to_report()
    {
        var user = await SeedUserAsync();

        var body = await CreateSut().ComposeAsync(user, Now, CancellationToken.None);

        body.Should().BeNull();
    }

    [Fact]
    public async Task Reports_no_deadlines_when_only_grades_exist()
    {
        var user = await SeedUserAsync();
        var maths = Subject.Create("Maths", null, Now);
        _db.Subjects.Add(maths);
        await _db.SaveChangesAsync();
        _db.Grades.Add(Grade.Add(user.Id, maths.Id, 3, 1m, null, Now.AddDays(-1), user.Id, Now));
        await _db.SaveChangesAsync();

        var body = await CreateSut().ComposeAsync(user, Now, CancellationToken.None);

        body.Should().NotBeNull();
        body!.Should().Contain("No deadlines due today");
        body.Should().Contain("Maths: 3.0");
    }
}
