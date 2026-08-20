using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Domain.Tests.Users;

public class InviteCodeTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Fresh_code_can_be_redeemed()
    {
        var code = InviteCode.Create("ABC", UserRole.Student, Now);

        code.CanBeRedeemed(Now).Should().BeTrue();
        code.IsUsed.Should().BeFalse();
    }

    [Fact]
    public void Expired_code_cannot_be_redeemed()
    {
        var code = InviteCode.Create("ABC", UserRole.Student, Now, expiresAt: Now.AddHours(-1));

        code.IsExpired(Now).Should().BeTrue();
        code.CanBeRedeemed(Now).Should().BeFalse();
    }

    [Fact]
    public void Redeemed_code_is_marked_used_and_cannot_be_redeemed_again()
    {
        var code = InviteCode.Create("ABC", UserRole.Admin, Now);
        var userId = Guid.NewGuid();

        code.Redeem(userId);

        code.IsUsed.Should().BeTrue();
        code.UsedByUserId.Should().Be(userId);
        code.CanBeRedeemed(Now).Should().BeFalse();
    }

    [Fact]
    public void Redeeming_an_already_used_code_throws()
    {
        var code = InviteCode.Create("ABC", UserRole.Admin, Now);
        code.Redeem(Guid.NewGuid());

        var act = () => code.Redeem(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }
}
