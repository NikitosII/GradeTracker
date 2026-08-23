using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Domain.Common;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Queries.GetSystemStatus;

internal sealed class GetSystemStatusQueryHandler : IQueryHandler<GetSystemStatusQuery, SystemStatusDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetSystemStatusQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<SystemStatusDto>> Handle(GetSystemStatusQuery request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<SystemStatusDto>(gate.Error);
        }

        var totalUsers = await _db.Users.AsNoTracking().CountAsync(cancellationToken);
        var admins = await _db.Users.AsNoTracking().CountAsync(u => u.Role == UserRole.Admin, cancellationToken);
        var subjects = await _db.Subjects.AsNoTracking().CountAsync(cancellationToken);
        var activeSubjects = await _db.Subjects.AsNoTracking().CountAsync(s => s.IsActive, cancellationToken);
        var grades = await _db.Grades.AsNoTracking().CountAsync(cancellationToken);
        var assignments = await _db.Assignments.AsNoTracking().CountAsync(cancellationToken);
        var inviteCodes = await _db.InviteCodes.AsNoTracking().CountAsync(cancellationToken);
        var unusedInviteCodes = await _db.InviteCodes.AsNoTracking().CountAsync(c => c.UsedByUserId == null, cancellationToken);
        var auditEntries = await _db.AuditLogs.AsNoTracking().CountAsync(cancellationToken);

        var status = new SystemStatusDto(
            totalUsers,
            admins,
            totalUsers - admins,
            subjects,
            activeSubjects,
            grades,
            assignments,
            inviteCodes,
            unusedInviteCodes,
            auditEntries,
            _clock.UtcNow);

        return Result.Success(status);
    }
}
