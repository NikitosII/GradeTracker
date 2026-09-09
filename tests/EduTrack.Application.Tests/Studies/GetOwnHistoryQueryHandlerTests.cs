using EduTrack.Application.Admin;
using EduTrack.Application.Studies.Queries.GetOwnHistory;
using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class GetOwnHistoryQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();

    private GetOwnHistoryQueryHandler CreateSut() => new(_db);

    private async Task<User> SeedUserAsync(long telegramId = 500)
    {
        var user = User.Register(telegramId, "nick", "Ada", null, UserRole.Student, Now);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Returns_only_the_callers_entries_newest_first()
    {
        var user = await SeedUserAsync();
        var other = await SeedUserAsync(600);

        _db.AuditLogs.Add(AuditLog.Create(user.Id, AuditActions.GradeAdded, AuditEntities.Grade, "g1", null, "Math: 5", Now.AddMinutes(-10)));
        _db.AuditLogs.Add(AuditLog.Create(user.Id, AuditActions.DeadlineCreated, AuditEntities.Deadline, "d1", null, "Lab", Now));
        _db.AuditLogs.Add(AuditLog.Create(other.Id, AuditActions.GradeAdded, AuditEntities.Grade, "g2", null, "Physics: 4", Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new GetOwnHistoryQuery(user.TelegramUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Action.Should().Be(AuditActions.DeadlineCreated); // newest first
        result.Value.Items[0].Detail.Should().Be("Lab");
        result.Value.Items.Should().OnlyContain(i => i.Detail != "Physics: 4");
    }

    [Fact]
    public async Task Paginates()
    {
        var user = await SeedUserAsync();
        for (var i = 0; i < 10; i++)
        {
            _db.AuditLogs.Add(AuditLog.Create(user.Id, AuditActions.GradeAdded, AuditEntities.Grade, $"g{i}", null, $"Math: {i}", Now.AddMinutes(-i)));
        }
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(new GetOwnHistoryQuery(user.TelegramUserId, Page: 1, PageSize: 4), CancellationToken.None);

        result.Value.Items.Should().HaveCount(4);
        result.Value.TotalCount.Should().Be(10);
        result.Value.TotalPages.Should().Be(3);
        result.Value.HasNext.Should().BeTrue();
    }

    [Fact]
    public async Task Fails_when_user_not_bound()
    {
        var result = await CreateSut().Handle(new GetOwnHistoryQuery(999), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotBound);
    }
}
