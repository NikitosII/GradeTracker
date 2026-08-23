using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Queries.GetAuditLog;

internal sealed class GetAuditLogQueryHandler : IQueryHandler<GetAuditLogQuery, AuditLogPageDto>
{
    private const int MaxPageSize = 20;

    private readonly IApplicationDbContext _db;

    public GetAuditLogQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<AuditLogPageDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<AuditLogPageDto>(gate.Error);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var totalCount = await _db.AuditLogs.AsNoTracking().CountAsync(cancellationToken);

        // Left-join the actor so entries survive even if the user record is gone.
        var rows = await (
                from a in _db.AuditLogs.AsNoTracking()
                orderby a.CreatedAt descending, a.Id
                join u in _db.Users on a.UserId equals u.Id into actors
                from actor in actors.DefaultIfEmpty()
                select new
                {
                    a.Id,
                    a.Action,
                    a.EntityType,
                    a.EntityId,
                    a.CreatedAt,
                    Username = actor == null ? null : actor.Username,
                    FirstName = actor == null ? null : actor.FirstName,
                    LastName = actor == null ? null : actor.LastName,
                })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AuditLogEntryDto(
                r.Id,
                ActorName(r.Username, r.FirstName, r.LastName),
                r.Action,
                r.EntityType,
                r.EntityId,
                r.CreatedAt))
            .ToList();

        return Result.Success(new AuditLogPageDto(items, page, pageSize, totalCount));
    }

    private static string? ActorName(string? username, string? firstName, string? lastName)
    {
        var full = string.Join(' ', new[] { firstName, lastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (!string.IsNullOrWhiteSpace(full))
        {
            return full;
        }

        return string.IsNullOrWhiteSpace(username) ? null : "@" + username;
    }
}
