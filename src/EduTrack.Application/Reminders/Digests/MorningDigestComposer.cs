using System.Globalization;
using System.Text;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Localization;
using EduTrack.Application.Studies.Stats;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Reminders.Digests;

internal sealed class MorningDigestComposer : IMorningDigestComposer
{
    private readonly IApplicationDbContext _db;
    private readonly ITranslator _translator;

    public MorningDigestComposer(IApplicationDbContext db, ITranslator translator)
    {
        _db = db;
        _translator = translator;
    }

    public async Task<string?> ComposeAsync(User user, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var tz = ResolveTimeZone(user.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified), tz);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localNow.Date.AddDays(1), DateTimeKind.Unspecified), tz);

        var today = await (
            from a in _db.Assignments.AsNoTracking()
            join s in _db.Subjects.AsNoTracking() on a.SubjectId equals s.Id
            where a.OwnerUserId == user.Id && a.DueAtUtc >= dayStartUtc && a.DueAtUtc < dayEndUtc
            orderby a.DueAtUtc
            select new { s.Name, a.Title, a.DueAtUtc }).ToListAsync(cancellationToken);

        // Weighted GPA (spec §22.1), consistent with /grades and /stats.
        var gradeRows = await (
            from g in _db.Grades.AsNoTracking()
            join s in _db.Subjects.AsNoTracking() on g.SubjectId equals s.Id
            where g.StudentUserId == user.Id
            select new { Subject = s.Name, g.Value, g.Weight }).ToListAsync(cancellationToken);

        var averages = gradeRows
            .GroupBy(r => r.Subject)
            .OrderBy(grp => grp.Key)
            .Select(grp => new
            {
                Subject = grp.Key,
                Average = GpaCalculator.WeightedAverage(grp.Select(r => (r.Value, r.Weight))) ?? 0,
            })
            .ToList();

        if (today.Count == 0 && averages.Count == 0)
        {
            return null;
        }

        var language = user.Language;
        var body = new StringBuilder();

        body.Append(T(language, TextKeys.DigestToday));
        if (today.Count == 0)
        {
            body.Append('\n').Append(T(language, TextKeys.DigestNoDeadlines));
        }
        else
        {
            var index = 1;
            foreach (var item in today)
            {
                var due = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(item.DueAtUtc, DateTimeKind.Utc), tz);
                body.Append(CultureInfo.InvariantCulture, $"\n{index}. {item.Name} - {item.Title} ({due:HH:mm})");
                index++;
            }
        }

        if (averages.Count > 0)
        {
            body.Append("\n\n").Append(T(language, TextKeys.DigestAverage));
            foreach (var avg in averages)
            {
                body.Append(CultureInfo.InvariantCulture, $"\n{avg.Subject}: {avg.Average:0.0}");
            }
        }

        return body.ToString();
    }

    private string T(string? language, string key) => _translator.Find(language, key) ?? key;

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
