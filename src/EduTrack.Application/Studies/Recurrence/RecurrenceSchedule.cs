namespace EduTrack.Application.Studies.Recurrence;

/// <summary>
/// Expands a recurrence rule into concrete occurrence datetimes.
/// </summary>
public static class RecurrenceSchedule
{
    /// <summary>Upper bound on how many occurrences a single series may materialise.</summary>
    public const int MaxCount = 52;

    public static IReadOnlyList<DateTime> Occurrences(
        DateTime firstDueUtc,
        RecurrenceFrequency frequency,
        int interval,
        int count)
    {
        var step = Math.Max(1, interval);
        var total = Math.Clamp(count, 1, MaxCount);

        var occurrences = new List<DateTime>(total);
        for (var i = 0; i < total; i++)
        {
            occurrences.Add(Advance(firstDueUtc, frequency, step * i));
        }

        return occurrences;
    }

    private static DateTime Advance(DateTime anchor, RecurrenceFrequency frequency, int steps) => frequency switch
    {
        RecurrenceFrequency.Daily => anchor.AddDays(steps),
        RecurrenceFrequency.Weekly => anchor.AddDays(7 * steps),
        RecurrenceFrequency.Monthly => anchor.AddMonths(steps),
        _ => anchor.AddDays(steps),
    };
}
