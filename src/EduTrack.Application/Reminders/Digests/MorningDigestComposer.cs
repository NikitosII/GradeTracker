using System.Globalization;
using System.Text;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Reminders.Digests;

internal sealed class MorningDigestComposer : IMorningDigestComposer
{
    private readonly IApplicationDbContext _db;

    public MorningDigestComposer(IApplicationDbContext db)
    {
        _db = db;
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

        var averages = await (
            from g in _db.Grades.AsNoTracking()
            join s in _db.Subjects.AsNoTracking() on g.SubjectId equals s.Id
            where g.StudentUserId == user.Id
            group g.Value by s.Name into bySubject
            orderby bySubject.Key
            select new { Subject = bySubject.Key, Average = bySubject.Average() }).ToListAsync(cancellationToken);

        if (today.Count == 0 && averages.Count == 0)
        {
            return null;
        }

        var body = new StringBuilder();

        body.Append("Today:");
        if (today.Count == 0)
        {
            body.Append("\nNo deadlines due today.");
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
            body.Append("\n\nAverage score:");
            foreach (var avg in averages)
            {
                body.Append(CultureInfo.InvariantCulture, $"\n{avg.Subject}: {avg.Average:0.0}");
            }
        }

        return body.ToString();
    }

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
