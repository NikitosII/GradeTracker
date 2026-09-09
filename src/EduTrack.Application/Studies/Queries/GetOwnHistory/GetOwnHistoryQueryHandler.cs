using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Admin;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.History;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetOwnHistory;

internal sealed class GetOwnHistoryQueryHandler : IQueryHandler<GetOwnHistoryQuery, StudentHistoryPageDto>
{
    private const int MaxPageSize = 20;

    // Only the student's own academic changes belong in /history (not admin/settings actions).
    private static readonly string[] StudentActions =
    {
        AuditActions.GradeAdded,
        AuditActions.GradeUpdated,
        AuditActions.DeadlineCreated,
        AuditActions.DeadlineUpdated,
    };

    private readonly IApplicationDbContext _db;

    public GetOwnHistoryQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<StudentHistoryPageDto>> Handle(GetOwnHistoryQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<StudentHistoryPageDto>(UserErrors.NotBound);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var owned = _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId == user.Id && StudentActions.Contains(a.Action));

        var totalCount = await owned.CountAsync(cancellationToken);

        var items = await owned
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new StudentHistoryEntryDto(a.Action, a.EntityType, a.NewValue ?? a.OldValue, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new StudentHistoryPageDto(items, page, pageSize, totalCount));
    }
}
