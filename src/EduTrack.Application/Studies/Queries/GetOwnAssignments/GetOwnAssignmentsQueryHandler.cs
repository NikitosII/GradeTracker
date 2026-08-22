using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetOwnAssignments;

internal sealed class GetOwnAssignmentsQueryHandler : IQueryHandler<GetOwnAssignmentsQuery, AssignmentsPageDto>
{
    private const int MaxPageSize = 20;

    private readonly IApplicationDbContext _db;

    public GetOwnAssignmentsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<AssignmentsPageDto>> Handle(GetOwnAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AssignmentsPageDto>(UserErrors.NotBound);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var owned = _db.Assignments
            .AsNoTracking()
            .Where(a => a.OwnerUserId == user.Id);

        if (request.SubjectId is { } subjectId)
        {
            owned = owned.Where(a => a.SubjectId == subjectId);
        }

        if (request.FromUtc is { } fromUtc)
        {
            owned = owned.Where(a => a.DueAtUtc >= fromUtc);
        }

        if (request.ToUtc is { } toUtc)
        {
            owned = owned.Where(a => a.DueAtUtc <= toUtc);
        }

        var totalCount = await owned.CountAsync(cancellationToken);

        var items = await (
                from a in owned
                join s in _db.Subjects on a.SubjectId equals s.Id
                orderby a.DueAtUtc, a.Id
                select new AssignmentDto(a.Id, a.SubjectId, s.Name, a.Type, a.Title, a.Description, a.DueAtUtc))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result.Success(new AssignmentsPageDto(items, page, pageSize, totalCount));
    }
}
