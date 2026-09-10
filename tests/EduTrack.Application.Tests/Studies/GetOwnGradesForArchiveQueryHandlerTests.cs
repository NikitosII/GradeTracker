using EduTrack.Application.Studies.Queries.GetOwnGradesForArchive;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GetOwnGradesForArchiveQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private GetOwnGradesForArchiveQueryHandler CreateSut() => new(_db);

    private async Task SeedAsync()
    {
        var user = User.Register(100, "nick", "Ada", "Lovelace", UserRole.Student, Now);
        var subject = Subject.Create("Physics", null, Now);
        _db.Users.Add(user);
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var active = Grade.Add(user.Id, subject.Id, 5, 1m, null, Now.AddDays(-1), user.Id, Now);
        var archived = Grade.Add(user.Id, subject.Id, 3, 1m, null, Now.AddDays(-30), user.Id, Now);
        archived.Archive(user.Id, Now);

        _db.Grades.AddRange(active, archived);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_only_active_grades_when_not_archived()
    {
        await SeedAsync();

        var result = await CreateSut().Handle(new GetOwnGradesForArchiveQuery(100, Archived: false), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Primary.Should().Be("5");
    }

    [Fact]
    public async Task Returns_only_archived_grades_when_archived()
    {
        await SeedAsync();

        var result = await CreateSut().Handle(new GetOwnGradesForArchiveQuery(100, Archived: true), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Primary.Should().Be("3");
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(new GetOwnGradesForArchiveQuery(999, Archived: false), CancellationToken.None);

        result.Error.Should().Be(UserErrors.NotBound);
    }
}
