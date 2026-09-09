using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Studies.Stats;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetStudentTrends;

internal sealed class GetStudentTrendsQueryHandler : IQueryHandler<GetStudentTrendsQuery, StudentTrendsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetStudentTrendsQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<StudentTrendsDto>> Handle(GetStudentTrendsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<StudentTrendsDto>(UserErrors.NotBound);
        }

        var now = _clock.UtcNow;
        var currentFrom = now.AddDays(-30);
        var previousFrom = now.AddDays(-60);

        var grades = await (
                from g in _db.Grades.AsNoTracking()
                where g.StudentUserId == user.Id && g.OccurredAt >= previousFrom
                join s in _db.Subjects on g.SubjectId equals s.Id
                select new { SubjectName = s.Name, g.Value, g.Weight, g.OccurredAt })
            .ToListAsync(cancellationToken);

        var subjects = grades
            .GroupBy(g => g.SubjectName)
            .OrderBy(grp => grp.Key)
            .Select(grp =>
            {
                var current = grp.Where(g => g.OccurredAt >= currentFrom).ToList();
                var previous = grp.Where(g => g.OccurredAt < currentFrom).ToList();

                var currentAvg = GpaCalculator.WeightedAverage(current.Select(g => (g.Value, g.Weight)));
                var previousAvg = GpaCalculator.WeightedAverage(previous.Select(g => (g.Value, g.Weight)));

                double? delta = currentAvg is { } c && previousAvg is { } p ? Math.Round(c - p, 2) : null;

                return new SubjectTrendDto(grp.Key, currentAvg, previousAvg, delta, current.Count);
            })
            .ToList();

        return Result.Success(new StudentTrendsDto(subjects));
    }
}
