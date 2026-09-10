using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.Archive;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetOwnDeadlinesForArchive;

internal sealed class GetOwnDeadlinesForArchiveQueryHandler : IQueryHandler<GetOwnDeadlinesForArchiveQuery, ArchivePageDto>
{
    private const int MaxPageSize = 20;

    private readonly IApplicationDbContext _db;

    public GetOwnDeadlinesForArchiveQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ArchivePageDto>> Handle(GetOwnDeadlinesForArchiveQuery request, CancellationToken cancellationToken)
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
        var owned = _db.Assignments.AsNoTracking().IgnoreQueryFilters()
            .Where(a => a.OwnerUserId == user.Id && !a.IsDeleted && a.IsArchived == request.Archived);

        var totalCount = await owned.CountAsync(cancellationToken);

        var ordered = request.Archived
            ? owned.OrderByDescending(a => a.ArchivedAt).ThenByDescending(a => a.DueAtUtc)
            : owned.OrderBy(a => a.DueAtUtc);

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.DueAtUtc,
                SubjectName = _db.Subjects.Where(s => s.Id == a.SubjectId).Select(s => s.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ArchiveItemDto(
                r.Id,
                r.Title,
                $"{r.DueAtUtc:yyyy-MM-dd HH:mm} UTC · {r.SubjectName}"))
            .ToList();

        return Result.Success(new ArchivePageDto(items, page, pageSize, totalCount));
    }
}
