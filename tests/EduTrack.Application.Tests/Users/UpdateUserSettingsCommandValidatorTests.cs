using EduTrack.Application.Users.Commands.UpdateSettings;
using FluentAssertions;

namespace EduTrack.Application.Tests.Users;

public class UpdateUserSettingsCommandValidatorTests
{
    private readonly UpdateUserSettingsCommandValidator _validator = new();

    private static UpdateUserSettingsCommand Valid() => new(
        TelegramUserId: 500,
        TimeZone: "UTC",
        Language: "ru",
        NotificationsEnabled: true,
        MorningDigestEnabled: true,
        Reminder24hEnabled: true,
        Reminder2hEnabled: true,
        QuietHoursStart: null,
        QuietHoursEnd: null);

    [Fact]
    public void Accepts_a_valid_command()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_unknown_time_zone()
    {
        var result = _validator.Validate(Valid() with { TimeZone = "Mars/Olympus" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_unsupported_language()
    {
        var result = _validator.Validate(Valid() with { Language = "fr" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_out_of_range_quiet_hour()
    {
        var result = _validator.Validate(Valid() with { QuietHoursStart = 24, QuietHoursEnd = 7 });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_half_set_quiet_hours()
    {
        var result = _validator.Validate(Valid() with { QuietHoursStart = 22, QuietHoursEnd = null });

        result.IsValid.Should().BeFalse();
    }
}
