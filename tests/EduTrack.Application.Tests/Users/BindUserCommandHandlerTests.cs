using EduTrack.Application.Tests.TestSupport;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Commands.BindUser;
using EduTrack.Domain.Common;
using EduTrack.Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.Users;

public class BindUserCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly TestApplicationDbContext _db = TestApplicationDbContext.CreateInMemory();
    private readonly FixedClock _clock = new(Now);

    private BindUserCommandHandler CreateSut() => new(_db, _clock);

    private async Task SeedCodeAsync(InviteCode code)
    {
        _db.InviteCodes.Add(code);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Valid_code_creates_user_with_role_and_marks_code_used()
    {
        await SeedCodeAsync(InviteCode.Create("ADMIN-1", UserRole.Admin, Now));

        var result = await CreateSut().Handle(
            new BindUserCommand(100, "nick", "Ada", "Lovelace", "ADMIN-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(nameof(UserRole.Admin));

        var user = await _db.Users.SingleAsync();
        user.TelegramUserId.Should().Be(100);
        user.Role.Should().Be(UserRole.Admin);

        var code = await _db.InviteCodes.SingleAsync();
        code.IsUsed.Should().BeTrue();
        code.UsedByUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Unknown_code_fails_with_invalid_code()
    {
        var result = await CreateSut().Handle(
            new BindUserCommand(100, null, "Ada", null, "NOPE"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCode);
        (await _db.Users.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Expired_code_fails_with_code_not_usable()
    {
        await SeedCodeAsync(InviteCode.Create("OLD", UserRole.Student, Now, expiresAt: Now.AddDays(-1)));

        var result = await CreateSut().Handle(
            new BindUserCommand(100, null, "Ada", null, "OLD"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.CodeNotUsable);
    }

    [Fact]
    public async Task Already_bound_account_fails_with_already_bound()
    {
        await SeedCodeAsync(InviteCode.Create("CODE-1", UserRole.Student, Now));
        _db.Users.Add(User.Register(100, "nick", "Ada", null, UserRole.Student, Now));
        await _db.SaveChangesAsync();

        var result = await CreateSut().Handle(
            new BindUserCommand(100, "nick", "Ada", null, "CODE-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.AlreadyBound);
    }
}
