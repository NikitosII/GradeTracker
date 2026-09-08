using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Recurrence;
using FluentAssertions;

namespace EduTrack.Application.Tests.Studies;

public class RecurrenceScheduleTests
{
    private static readonly DateTime First = new(2026, 9, 4, 18, 0, 0, DateTimeKind.Utc); // a Friday

    [Fact]
    public void Weekly_steps_by_seven_days()
    {
        var occurrences = RecurrenceSchedule.Occurrences(First, RecurrenceFrequency.Weekly, 1, 3);

        occurrences.Should().Equal(
            First,
            First.AddDays(7),
            First.AddDays(14));
    }

    [Fact]
    public void Daily_honours_interval()
    {
        var occurrences = RecurrenceSchedule.Occurrences(First, RecurrenceFrequency.Daily, 2, 3);

        occurrences.Should().Equal(First, First.AddDays(2), First.AddDays(4));
    }

    [Fact]
    public void Monthly_steps_by_month()
    {
        var occurrences = RecurrenceSchedule.Occurrences(First, RecurrenceFrequency.Monthly, 1, 3);

        occurrences.Should().Equal(First, First.AddMonths(1), First.AddMonths(2));
    }

    [Fact]
    public void First_occurrence_is_the_anchor()
    {
        RecurrenceSchedule.Occurrences(First, RecurrenceFrequency.Weekly, 1, 1)
            .Should().ContainSingle().Which.Should().Be(First);
    }

    [Fact]
    public void Count_is_capped_at_the_maximum()
    {
        RecurrenceSchedule.Occurrences(First, RecurrenceFrequency.Daily, 1, 1000)
            .Should().HaveCount(RecurrenceSchedule.MaxCount);
    }
}
