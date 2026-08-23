using EduTrack.Application.Admin;
using EduTrack.Application.Admin.Queries.GetUsers;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Admin;

public class GetUsersQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private GetUsersQueryHandler CreateSut() => new(_db);

    [Fact]
    public async Task Returns_users_for_admin()
    {
        _db.Users.Add(User.Register(100, "root", "Root", null, UserRole.Admin, Now));
        _db.Users.Add(User.Register(200, "bob", "Bob", "Jones", UserRole.Student, Now.AddMinutes(1)));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new GetUsersQuery(100, 1, 8), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().Contain(u => u.FullName == "Bob Jones" && u.Role == nameof(UserRole.Student));
    }

    [Fact]
    public async Task Fails_when_caller_is_not_admin()
    {
        _db.Users.Add(User.Register(200, "bob", "Bob", null, UserRole.Student, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new GetUsersQuery(200, 1, 8), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErrors.NotAdmin);
    }
}
