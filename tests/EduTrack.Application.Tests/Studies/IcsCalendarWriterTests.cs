using System.Text;
using EduTrack.Application.Studies.Ics;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class IcsCalendarWriterTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Writes_a_wellformed_calendar_with_crlf()
    {
        var events = new[]
        {
            new IcsEvent("abc@edutrack", new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc), "[Exam] Calculus", "Subject: Math"),
        };

        var ics = IcsCalendarWriter.Write(events, Now);

        ics.Should().StartWith("BEGIN:VCALENDAR\r\n");
        ics.Should().EndWith("END:VCALENDAR\r\n");
        ics.Should().Contain("VERSION:2.0\r\n");
        ics.Should().Contain("BEGIN:VEVENT\r\n");
        ics.Should().Contain("UID:abc@edutrack\r\n");
        ics.Should().Contain("DTSTAMP:20260906T103000Z\r\n");
        ics.Should().Contain("DTSTART:20260910T180000Z\r\n");
        ics.Should().Contain("SUMMARY:[Exam] Calculus\r\n");
        ics.Should().Contain("DESCRIPTION:Subject: Math\r\n");
        ics.Should().Contain("BEGIN:VALARM\r\n");
        ics.Should().Contain("TRIGGER:-P1D\r\n");
    }

    [Fact]
    public void Escapes_special_characters_in_text_values()
    {
        var events = new[]
        {
            new IcsEvent("x@edutrack", Now, "Read ch. 1, 2; done", "line1\nline2"),
        };

        var ics = IcsCalendarWriter.Write(events, Now);

        ics.Should().Contain(@"SUMMARY:Read ch. 1\, 2\; done");
        ics.Should().Contain(@"DESCRIPTION:line1\nline2");
    }

    [Fact]
    public void Folds_lines_longer_than_75_octets()
    {
        var longTitle = new string('A', 200);
        var events = new[] { new IcsEvent("x@edutrack", Now, longTitle, null) };

        var ics = IcsCalendarWriter.Write(events, Now);

        foreach (var line in ics.Split("\r\n"))
        {
            Encoding.UTF8.GetByteCount(line).Should().BeLessThanOrEqualTo(75);
        }

        // Continuation lines are marked by a leading space.
        ics.Should().Contain("\r\n A");
    }

    [Fact]
    public void Empty_event_list_still_produces_a_valid_calendar()
    {
        var ics = IcsCalendarWriter.Write(Array.Empty<IcsEvent>(), Now);

        ics.Should().Contain("BEGIN:VCALENDAR\r\n");
        ics.Should().Contain("END:VCALENDAR\r\n");
        ics.Should().NotContain("BEGIN:VEVENT");
    }
}
