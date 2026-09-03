using EduTrack.Application.Notifications;
using FluentAssertions;

namespace EduTrack.Application.Tests.Notifications;

public class QuietHoursTests
{
    private static DateTime Utc(int hour) => new(2026, 8, 23, hour, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(23, true)]  // inside a 22->8 wrap window
    [InlineData(3, true)]
    [InlineData(7, true)]
    [InlineData(8, false)] // end is exclusive
    [InlineData(12, false)]
    [InlineData(21, false)]
    [InlineData(22, true)] // start is inclusive
    public void Wrapping_window_is_evaluated_correctly(int hourUtc, bool expected)
    {
        QuietHours.IsWithin(Utc(hourUtc), "UTC", 22, 8).Should().Be(expected);
    }

    [Fact]
    public void Equal_start_and_end_is_never_quiet()
    {
        QuietHours.IsWithin(Utc(3), "UTC", 8, 8).Should().BeFalse();
    }

    [Fact]
    public void Unknown_timezone_falls_back_to_utc()
    {
        QuietHours.IsWithin(Utc(23), "Not/AZone", 22, 8).Should().BeTrue();
    }

    [Fact]
    public void Non_wrapping_window_is_evaluated_correctly()
    {
        QuietHours.IsWithin(Utc(1), "UTC", 0, 6).Should().BeTrue();
        QuietHours.IsWithin(Utc(6), "UTC", 0, 6).Should().BeFalse();
    }
}
