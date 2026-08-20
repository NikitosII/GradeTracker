using EduTrack.Domain.Users;
using FluentAssertions;

namespace EduTrack.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_sets_defaults()
    {
        var user = User.Register(555, "nick", "Ada", "Lovelace", UserRole.Student, Now);

        user.Id.Should().NotBeEmpty();
        user.TelegramUserId.Should().Be(555);
        user.Role.Should().Be(UserRole.Student);
        user.TimeZone.Should().Be(User.DefaultTimeZone);
        user.Language.Should().Be(User.DefaultLanguage);
        user.IsNotificationsEnabled.Should().BeTrue();
        user.CreatedAt.Should().Be(Now);
        user.FullName.Should().Be("Ada Lovelace");
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("Ada", null, "Ada")]
    [InlineData(null, "Lovelace", "Lovelace")]
    public void FullName_joins_available_parts(string? first, string? last, string? expected)
    {
        var user = User.Register(1, null, first, last, UserRole.Student, Now);

        user.FullName.Should().Be(expected);
    }
}
