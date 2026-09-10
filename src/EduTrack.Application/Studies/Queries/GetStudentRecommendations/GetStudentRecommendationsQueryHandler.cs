using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Studies.Recommendations;
using EduTrack.Application.Studies.Stats;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetStudentRecommendations;

internal sealed class GetStudentRecommendationsQueryHandler
    : IQueryHandler<GetStudentRecommendationsQuery, StudentRecommendationsDto>
{
    private const int MaxRecommendations = 5;
    private const int WeekWindowDays = 7;
    private const int MinWeekWorkload = 2;
    private const int MaxFalling = 2;
    private const double FallingDelta = -0.3;
    private const double RisingDelta = 0.3;
    private const double LowSubjectThreshold = 3.5;

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetStudentRecommendationsQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<StudentRecommendationsDto>> Handle(GetStudentRecommendationsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<StudentRecommendationsDto>(UserErrors.NotBound);
        }

        var now = _clock.UtcNow;

        var grades = await (
                from g in _db.Grades.AsNoTracking()
                join s in _db.Subjects on g.SubjectId equals s.Id
                where g.StudentUserId == user.Id
                select new { SubjectName = s.Name, g.Value, g.Weight, g.OccurredAt })
            .ToListAsync(cancellationToken);

        var deadlines = await (
                from a in _db.Assignments.AsNoTracking()
                join s in _db.Subjects on a.SubjectId equals s.Id
                where a.OwnerUserId == user.Id
                select new { SubjectName = s.Name, a.Title, a.DueAtUtc })
            .ToListAsync(cancellationToken);

        var recommendations = new List<RecommendationDto>();
        var flaggedSubjects = new HashSet<string>();

        // 1. Overdue deadlines.
        var overdue = deadlines.Count(d => d.DueAtUtc < now);
        if (overdue > 0)
        {
            recommendations.Add(new RecommendationDto(RecommendationKind.OverdueDeadlines, null, overdue, null, null, null, null));
        }

        // 2. The most urgent upcoming deadline.
        var nearest = deadlines
            .Where(d => d.DueAtUtc >= now)
            .OrderBy(d => d.DueAtUtc)
            .FirstOrDefault();
        if (nearest is not null)
        {
            recommendations.Add(new RecommendationDto(RecommendationKind.UrgentDeadline, nearest.SubjectName, 0, null, null, nearest.Title, nearest.DueAtUtc));
        }

        // 3. Workload for the coming week (only when there's more than the single urgent one).
        var weekEnd = now.AddDays(WeekWindowDays);
        var weekCount = deadlines.Count(d => d.DueAtUtc >= now && d.DueAtUtc < weekEnd);
        if (weekCount >= MinWeekWorkload)
        {
            recommendations.Add(new RecommendationDto(RecommendationKind.WeekWorkload, null, weekCount, null, null, null, null));
        }

        // Weighted 30d-vs-previous-30d dynamics, mirroring /trends.
        var currentFrom = now.AddDays(-30);
        var previousFrom = now.AddDays(-60);
        var windowed = grades
            .Where(g => g.OccurredAt >= previousFrom)
            .GroupBy(g => g.SubjectName)
            .Select(grp =>
            {
                var current = GpaCalculator.WeightedAverage(grp.Where(g => g.OccurredAt >= currentFrom).Select(g => (g.Value, g.Weight)));
                var previous = GpaCalculator.WeightedAverage(grp.Where(g => g.OccurredAt < currentFrom).Select(g => (g.Value, g.Weight)));
                double? delta = current is { } c && previous is { } p ? Math.Round(c - p, 2) : null;
                return new { Subject = grp.Key, Current = current, Previous = previous, Delta = delta };
            })
            .Where(x => x.Delta is not null)
            .ToList();

        // 4. Subjects whose average is slipping (worst first).
        foreach (var s in windowed.Where(x => x.Delta <= FallingDelta).OrderBy(x => x.Delta).Take(MaxFalling))
        {
            recommendations.Add(new RecommendationDto(RecommendationKind.FallingAverage, s.Subject, 0, s.Current, s.Previous, null, null));
            flaggedSubjects.Add(s.Subject);
        }

        // 5. The weakest subject overall (all-time weighted average), if not already flagged.
        var worstLow = grades
            .GroupBy(g => g.SubjectName)
            .Select(grp => new { Subject = grp.Key, Average = GpaCalculator.WeightedAverage(grp.Select(g => (g.Value, g.Weight))) })
            .Where(x => x.Average is not null && x.Average < LowSubjectThreshold && !flaggedSubjects.Contains(x.Subject))
            .OrderBy(x => x.Average)
            .FirstOrDefault();
        if (worstLow is not null)
        {
            recommendations.Add(new RecommendationDto(RecommendationKind.LowSubject, worstLow.Subject, 0, worstLow.Average, null, null, null));
            flaggedSubjects.Add(worstLow.Subject);
        }

        // 6. A subject that's improving nicely (encouragement), if not already flagged.
        var bestRising = windowed
            .Where(x => x.Delta >= RisingDelta && !flaggedSubjects.Contains(x.Subject))
            .OrderByDescending(x => x.Delta)
            .FirstOrDefault();
        if (bestRising is not null)
        {
            recommendations.Add(new RecommendationDto(RecommendationKind.RisingAverage, bestRising.Subject, 0, bestRising.Current, bestRising.Previous, null, null));
        }

        // 7. Onboarding nudge when there are no grades yet.
        if (grades.Count == 0)
        {
            recommendations.Add(new RecommendationDto(RecommendationKind.NoGradesYet, null, 0, null, null, null, null));
        }

        var top = recommendations.Take(MaxRecommendations).ToList();
        return Result.Success(new StudentRecommendationsDto(top));
    }
}
