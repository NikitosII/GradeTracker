using EduTrack.Application.Reminders;
using FluentAssertions;

namespace EduTrack.Application.Tests.Reminders;

public class ReminderCallbackTests
{
    [Theory]
    [InlineData(SnoozeOption.OneHour)]
    [InlineData(SnoozeOption.ThreeHours)]
    [InlineData(SnoozeOption.TomorrowMorning)]
    public void Snooze_payload_round_trips(SnoozeOption option)
    {
        var id = Guid.NewGuid();

        var data = ReminderCallback.Snooze(id, option);
        var parsed = ReminderCallback.TryParseSnooze(data, out var reminderId, out var parsedOption);

        parsed.Should().BeTrue();
        reminderId.Should().Be(id);
        parsedOption.Should().Be(option);
    }

    [Theory]
    [InlineData("gv:sub:123")]
    [InlineData("rmd:other:123:0")]
    [InlineData("rmd:snz:not-a-guid:0")]
    [InlineData("rmd:snz")]
    public void Rejects_unrelated_or_malformed_payloads(string data)
    {
        ReminderCallback.TryParseSnooze(data, out _, out _).Should().BeFalse();
    }
}
