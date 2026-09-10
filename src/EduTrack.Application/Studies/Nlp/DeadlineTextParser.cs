using System.Globalization;
using System.Text.RegularExpressions;
using EduTrack.Domain.Studies;

namespace EduTrack.Application.Studies.Nlp;

/// <summary>The best-effort interpretation of a free-text deadline (see <see cref="DeadlineTextParser"/>).</summary>
public sealed record ParsedDeadline(
    Guid? SubjectId,
    string? SubjectName,
    AssignmentType Type,
    DateTime? DueAtUtc,
    string Title);

/// <summary>
/// A rule-based, bilingual (RU/EN) parser that turns a phrase like
/// "Physics homework due Friday" into a candidate deadline. Heuristic by design;
/// callers confirm the result with the user before saving.
/// </summary>
public static class DeadlineTextParser
{
    private static readonly TimeOnly DefaultTime = new(23, 59);

    private static readonly (string Keyword, AssignmentType Type)[] TypeKeywords =
    {
        ("homework", AssignmentType.Homework), ("домаш", AssignmentType.Homework), ("дз", AssignmentType.Homework),
        ("exam", AssignmentType.Exam), ("экзамен", AssignmentType.Exam),
        ("project", AssignmentType.Project), ("проект", AssignmentType.Project),
        ("lab", AssignmentType.Lab), ("лаб", AssignmentType.Lab),
        ("quiz", AssignmentType.Quiz), ("опрос", AssignmentType.Quiz),
        ("test", AssignmentType.Test), ("контрольн", AssignmentType.Test), ("тест", AssignmentType.Quiz),
    };

    private static readonly (string Name, DayOfWeek Day)[] Weekdays =
    {
        ("monday", DayOfWeek.Monday), ("mon", DayOfWeek.Monday), ("понедельник", DayOfWeek.Monday), ("пн", DayOfWeek.Monday),
        ("tuesday", DayOfWeek.Tuesday), ("tue", DayOfWeek.Tuesday), ("вторник", DayOfWeek.Tuesday), ("вт", DayOfWeek.Tuesday),
        ("wednesday", DayOfWeek.Wednesday), ("wed", DayOfWeek.Wednesday), ("среда", DayOfWeek.Wednesday), ("ср", DayOfWeek.Wednesday),
        ("thursday", DayOfWeek.Thursday), ("thu", DayOfWeek.Thursday), ("четверг", DayOfWeek.Thursday), ("чт", DayOfWeek.Thursday),
        ("friday", DayOfWeek.Friday), ("fri", DayOfWeek.Friday), ("пятница", DayOfWeek.Friday), ("пятницу", DayOfWeek.Friday), ("пт", DayOfWeek.Friday),
        ("saturday", DayOfWeek.Saturday), ("sat", DayOfWeek.Saturday), ("суббота", DayOfWeek.Saturday), ("субботу", DayOfWeek.Saturday), ("сб", DayOfWeek.Saturday),
        ("sunday", DayOfWeek.Sunday), ("sun", DayOfWeek.Sunday), ("воскресенье", DayOfWeek.Sunday), ("вс", DayOfWeek.Sunday),
    };

    private static readonly Regex IsoDate = new(@"\b(\d{4})-(\d{2})-(\d{2})\b", RegexOptions.Compiled);
    private static readonly Regex DmyDate = new(@"\b(\d{1,2})\.(\d{1,2})(?:\.(\d{2,4}))?\b", RegexOptions.Compiled);
    private static readonly Regex InDays = new(@"\b(?:in|через)\s+(\d+)\s+(?:days?|дн\w*)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Time24 = new(@"\b(?:at|в)?\s*(\d{1,2}):(\d{2})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TimeAmPm = new(@"\b(\d{1,2})\s*(am|pm)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Filler = new(@"\b(due|by|on|at|in|к|до|на|в)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static ParsedDeadline Parse(
        string text,
        IReadOnlyList<SubjectDto> subjects,
        string? language,
        DateTime nowUtc,
        string? timeZoneId)
    {
        var original = (text ?? string.Empty).Trim();
        var lower = original.ToLowerInvariant();

        var subject = subjects
            .Where(s => s.IsActive && !string.IsNullOrWhiteSpace(s.Name) && lower.Contains(s.Name.ToLowerInvariant()))
            .OrderByDescending(s => s.Name.Length)
            .FirstOrDefault();

        var type = MatchType(lower);

        var tz = ResolveTimeZone(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);

        var (date, dateText) = MatchDate(lower, localNow);
        var (time, timeText) = MatchTime(lower);

        DateTime? dueAtUtc = null;
        if (date is { } d)
        {
            var localDue = d.Add((time ?? DefaultTime).ToTimeSpan());
            dueAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDue, DateTimeKind.Unspecified), tz);
        }

        var title = BuildTitle(original, subject?.Name, dateText, timeText);

        return new ParsedDeadline(subject?.Id, subject?.Name, type, dueAtUtc, title);
    }

    private static AssignmentType MatchType(string lower)
    {
        foreach (var (keyword, type) in TypeKeywords)
        {
            if (lower.Contains(keyword))
            {
                return type;
            }
        }

        return AssignmentType.Homework;
    }

    private static (DateTime? Date, string? MatchedText) MatchDate(string lower, DateTime localNow)
    {
        var iso = IsoDate.Match(lower);
        if (iso.Success
            && DateTime.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
        {
            return (isoDate.Date, iso.Value);
        }

        var dmy = DmyDate.Match(lower);
        if (dmy.Success)
        {
            var day = int.Parse(dmy.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(dmy.Groups[2].Value, CultureInfo.InvariantCulture);
            var year = dmy.Groups[3].Success ? NormalizeYear(int.Parse(dmy.Groups[3].Value, CultureInfo.InvariantCulture)) : localNow.Year;
            if (month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
            {
                return (new DateTime(year, month, day), dmy.Value);
            }
        }

        if (Contains(lower, "today", out var t1) || Contains(lower, "сегодня", out t1))
        {
            return (localNow.Date, t1);
        }

        if (Contains(lower, "tomorrow", out var t2) || Contains(lower, "завтра", out t2))
        {
            return (localNow.Date.AddDays(1), t2);
        }

        var inDays = InDays.Match(lower);
        if (inDays.Success && int.TryParse(inDays.Groups[1].Value, out var n))
        {
            return (localNow.Date.AddDays(n), inDays.Value);
        }

        foreach (var (name, day) in Weekdays)
        {
            if (Regex.IsMatch(lower, $@"\b{Regex.Escape(name)}\b"))
            {
                var daysAhead = ((int)day - (int)localNow.DayOfWeek + 7) % 7;
                return (localNow.Date.AddDays(daysAhead), name);
            }
        }

        return (null, null);
    }

    private static (TimeOnly? Time, string? MatchedText) MatchTime(string lower)
    {
        var ampm = TimeAmPm.Match(lower);
        if (ampm.Success && int.TryParse(ampm.Groups[1].Value, out var h12) && h12 is >= 1 and <= 12)
        {
            var pm = ampm.Groups[2].Value.Equals("pm", StringComparison.OrdinalIgnoreCase);
            var hour = pm ? (h12 % 12) + 12 : h12 % 12;
            return (new TimeOnly(hour, 0), ampm.Value);
        }

        var t24 = Time24.Match(lower);
        if (t24.Success
            && int.TryParse(t24.Groups[1].Value, out var hh) && hh is >= 0 and <= 23
            && int.TryParse(t24.Groups[2].Value, out var mm) && mm is >= 0 and <= 59)
        {
            return (new TimeOnly(hh, mm), t24.Value);
        }

        return (null, null);
    }

    private static string BuildTitle(string original, string? subjectName, string? dateText, string? timeText)
    {
        var title = original;
        foreach (var token in new[] { subjectName, dateText, timeText })
        {
            if (!string.IsNullOrEmpty(token))
            {
                var idx = title.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    title = title.Remove(idx, token.Length);
                }
            }
        }

        title = Filler.Replace(title, " ");
        title = Whitespace.Replace(title, " ").Trim(' ', ',', '.', '-', ':');

        return string.IsNullOrWhiteSpace(title) ? original : title;
    }

    private static bool Contains(string source, string token, out string matched)
    {
        if (source.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            matched = token;
            return true;
        }

        matched = string.Empty;
        return false;
    }

    private static int NormalizeYear(int year) => year < 100 ? 2000 + year : year;

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
