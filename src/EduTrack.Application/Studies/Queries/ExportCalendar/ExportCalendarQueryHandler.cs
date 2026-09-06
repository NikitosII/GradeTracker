using System.Text;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Studies.Ics;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.ExportCalendar;

internal sealed class ExportCalendarQueryHandler : IQueryHandler<ExportCalendarQuery, CalendarExportDto>
{
    private const string FileName = "edutrack-deadlines.ics";

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ExportCalendarQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<CalendarExportDto>> Handle(ExportCalendarQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<CalendarExportDto>(UserErrors.NotBound);
        }

        var items = await (
                from a in _db.Assignments.AsNoTracking()
                where a.OwnerUserId == user.Id
                join s in _db.Subjects on a.SubjectId equals s.Id
                orderby a.DueAtUtc, a.Id
                select new AssignmentDto(a.Id, a.SubjectId, s.Name, a.Type, a.Title, a.Description, a.DueAtUtc))
            .ToListAsync(cancellationToken);

        var events = items.Select(ToIcsEvent).ToList();
        var ics = IcsCalendarWriter.Write(events, _clock.UtcNow);

        var dto = new CalendarExportDto(FileName, Encoding.UTF8.GetBytes(ics), events.Count);
        return Result.Success(dto);
    }

    private static IcsEvent ToIcsEvent(AssignmentDto a)
    {
        var summary = $"[{a.Type}] {a.Title}";

        var description = string.IsNullOrWhiteSpace(a.Description)
            ? $"Subject: {a.SubjectName}"
            : $"Subject: {a.SubjectName}\n{a.Description}";

        return new IcsEvent($"{a.Id}@edutrack", a.DueAtUtc, summary, description);
    }
}
