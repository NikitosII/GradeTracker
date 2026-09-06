using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Studies.Stats;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetStudentStats;

internal sealed class GetStudentStatsQueryHandler : IQueryHandler<GetStudentStatsQuery, StudentStatsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetStudentStatsQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<StudentStatsDto>> Handle(GetStudentStatsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<StudentStatsDto>(UserErrors.NotBound);
        }

        var now = _clock.UtcNow;
        var weekFrom = now.AddDays(-7);
        var monthFrom = now.AddDays(-30);

        // Per-student grade volume is small, so pull the rows and aggregate in
        // memory with the shared weighted-GPA calculator.
        var grades = await (
                from g in _db.Grades.AsNoTracking()
                where g.StudentUserId == user.Id
                join s in _db.Subjects on g.SubjectId equals s.Id
                select new { SubjectName = s.Name, g.Value, g.Weight, g.OccurredAt })
            .ToListAsync(cancellationToken);

        var upcomingDeadlines = await _db.Assignments
            .AsNoTracking()
            .CountAsync(a => a.OwnerUserId == user.Id && a.DueAtUtc >= now, cancellationToken);

        var subjects = grades
            .GroupBy(g => g.SubjectName)
            .Select(grp => new SubjectStatDto(
                grp.Key,
                GpaCalculator.WeightedAverage(grp.Select(x => (x.Value, x.Weight))) ?? 0,
                grp.Count()))
            .OrderByDescending(s => s.Average)
            .ThenBy(s => s.SubjectName)
            .ToList();

        var worst = subjects.Count == 0 ? null : subjects[^1];

        var weekGrades = grades.Where(g => g.OccurredAt >= weekFrom).ToList();
        var monthGrades = grades.Where(g => g.OccurredAt >= monthFrom).ToList();

        var dto = new StudentStatsDto(
            OverallGpa: GpaCalculator.WeightedAverage(grades.Select(g => (g.Value, g.Weight))),
            WeekAverage: GpaCalculator.WeightedAverage(weekGrades.Select(g => (g.Value, g.Weight))),
            WeekCount: weekGrades.Count,
            MonthAverage: GpaCalculator.WeightedAverage(monthGrades.Select(g => (g.Value, g.Weight))),
            MonthCount: monthGrades.Count,
            Subjects: subjects,
            WorstSubject: worst?.SubjectName,
            WorstSubjectAverage: worst?.Average,
            UpcomingDeadlines: upcomingDeadlines,
            TotalGrades: grades.Count);

        return Result.Success(dto);
    }
}
