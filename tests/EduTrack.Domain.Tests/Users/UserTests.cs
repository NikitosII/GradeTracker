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
        user.MorningDigestEnabled.Should().BeTrue();
        user.Reminder24hEnabled.Should().BeTrue();
        user.Reminder2hEnabled.Should().BeTrue();
        user.QuietHoursStart.Should().BeNull();
        user.QuietHoursEnd.Should().BeNull();
        user.CreatedAt.Should().Be(Now);
        user.FullName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public void Setting_preferences_updates_fields_and_timestamp()
    {
        var user = User.Register(1, null, "Ada", null, UserRole.Student, Now);
        var later = Now.AddHours(1);

        user.SetTimeZone("Europe/Moscow", later);
        user.SetLanguage("en", later);
        user.SetMorningDigestEnabled(false, later);
        user.SetReminder24hEnabled(false, later);
        user.SetReminder2hEnabled(false, later);
        user.SetQuietHours(23, 7, later);

        user.TimeZone.Should().Be("Europe/Moscow");
        user.Language.Should().Be("en");
        user.MorningDigestEnabled.Should().BeFalse();
        user.Reminder24hEnabled.Should().BeFalse();
        user.Reminder2hEnabled.Should().BeFalse();
        user.QuietHoursStart.Should().Be(23);
        user.QuietHoursEnd.Should().Be(7);
        user.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Clearing_quiet_hours_sets_both_to_null()
    {
        var user = User.Register(1, null, "Ada", null, UserRole.Student, Now);
        user.SetQuietHours(22, 8, Now);

        user.SetQuietHours(null, null, Now);

        user.QuietHoursStart.Should().BeNull();
        user.QuietHoursEnd.Should().BeNull();
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
