using System.Globalization;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.Archive;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetOwnGradesForArchive;

internal sealed class GetOwnGradesForArchiveQueryHandler : IQueryHandler<GetOwnGradesForArchiveQuery, ArchivePageDto>
{
    private const int MaxPageSize = 20;

    private readonly IApplicationDbContext _db;

    public GetOwnGradesForArchiveQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ArchivePageDto>> Handle(GetOwnGradesForArchiveQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<ArchivePageDto>(UserErrors.NotBound);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        // The archived flag is part of the global filter, so bypass it and re-apply the pieces we want.
        var owned = _db.Grades.AsNoTracking().IgnoreQueryFilters()
            .Where(g => g.StudentUserId == user.Id && !g.IsDeleted && g.IsArchived == request.Archived);

        var totalCount = await owned.CountAsync(cancellationToken);

        var ordered = request.Archived
            ? owned.OrderByDescending(g => g.ArchivedAt).ThenByDescending(g => g.OccurredAt)
            : owned.OrderByDescending(g => g.OccurredAt);

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new
            {
                g.Id,
                g.Value,
                g.OccurredAt,
                SubjectName = _db.Subjects.Where(s => s.Id == g.SubjectId).Select(s => s.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ArchiveItemDto(
                r.Id,
                r.Value.ToString(CultureInfo.InvariantCulture),
                $"{r.OccurredAt:yyyy-MM-dd} · {r.SubjectName}"))
            .ToList();

        return Result.Success(new ArchivePageDto(items, page, pageSize, totalCount));
    }
}
