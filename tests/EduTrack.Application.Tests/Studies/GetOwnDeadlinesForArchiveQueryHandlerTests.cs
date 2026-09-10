using EduTrack.Application.Studies.Queries.GetOwnDeadlinesForArchive;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GetOwnDeadlinesForArchiveQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private GetOwnDeadlinesForArchiveQueryHandler CreateSut() => new(_db);

    private async Task<User> SeedAsync()
    {
        var user = User.Register(100, "nick", "Ada", "Lovelace", UserRole.Student, Now);
        var subject = Subject.Create("Physics", null, Now);
        _db.Users.Add(user);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var active = Assignment.Create(user.Id, subject.Id, AssignmentType.Lab, "Active lab", null, Now.AddDays(2), user.Id, Now);
        var archived = Assignment.Create(user.Id, subject.Id, AssignmentType.Exam, "Old exam", null, Now.AddDays(-5), user.Id, Now);
        archived.Archive(user.Id, Now);

        _db.Assignments.AddRange(active, archived);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Returns_only_active_deadlines_when_not_archived()
    {
        await SeedAsync();

        var result = await CreateSut().Handle(new GetOwnDeadlinesForArchiveQuery(100, Archived: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Primary.Should().Be("Active lab");
        result.Value.Items[0].Secondary.Should().Contain("Physics");
    }

    [Fact]
    public async Task Returns_only_archived_deadlines_when_archived()
    {
        await SeedAsync();

        var result = await CreateSut().Handle(new GetOwnDeadlinesForArchiveQuery(100, Archived: true), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Primary.Should().Be("Old exam");
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(new GetOwnDeadlinesForArchiveQuery(999, Archived: false), CancellationToken.None);

        result.Error.Should().Be(UserErrors.NotBound);
    }
}
