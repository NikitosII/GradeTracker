using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.Stats;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetOwnGrades;

internal sealed class GetOwnGradesQueryHandler : IQueryHandler<GetOwnGradesQuery, GradesPageDto>
{
    private const int MaxPageSize = 20;

    private readonly IApplicationDbContext _db;

    public GetOwnGradesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<GradesPageDto>> Handle(GetOwnGradesQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<GradesPageDto>(UserErrors.NotBound);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var owned = _db.Grades
            .AsNoTracking()
            .Where(g => g.StudentUserId == user.Id);

        if (request.SubjectId is { } subjectId)
        {
            owned = owned.Where(g => g.SubjectId == subjectId);
        }

        // Weighted GPA, consistent with /stats and the digest.
        var weights = await owned
            .Select(g => new { g.Value, g.Weight })
            .ToListAsync(cancellationToken);

        var totalCount = weights.Count;
        var average = GpaCalculator.WeightedAverage(weights.Select(w => (w.Value, w.Weight)));

        var items = await (
                from g in owned
                join s in _db.Subjects on g.SubjectId equals s.Id
                orderby g.OccurredAt descending, g.Id
                select new GradeDto(g.Id, g.SubjectId, s.Name, g.Value, g.Weight, g.Comment, g.OccurredAt))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result.Success(new GradesPageDto(request.SubjectId, items, page, pageSize, totalCount, average));
    }
}
