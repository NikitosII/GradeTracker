using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Admin;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Users;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Commands.SetOwnAssignmentArchived;

internal sealed class SetOwnAssignmentArchivedCommandHandler : ICommandHandler<SetOwnAssignmentArchivedCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SetOwnAssignmentArchivedCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> Handle(SetOwnAssignmentArchivedCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotBound);
        }

        // Bypass the global filter so an already-archived deadline can be found and restored.
        var assignment = await _db.Assignments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId && !a.IsDeleted, cancellationToken);

        if (assignment is null)
        {
            return Result.Failure(AssignmentErrors.NotFound);
        }

        if (assignment.OwnerUserId != user.Id)
        {
            return Result.Failure(AssignmentErrors.NotOwner);
        }

        if (assignment.IsArchived == request.Archived)
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        var detail = $"{assignment.Title} — {assignment.DueAtUtc:yyyy-MM-dd HH:mm} UTC";

        if (request.Archived)
        {
            assignment.Archive(user.Id, now);
        }
        else
        {
            assignment.Unarchive(user.Id, now);
        }

        _db.AuditLogs.Add(AuditLog.Create(
            user.Id,
            request.Archived ? AuditActions.DeadlineArchived : AuditActions.DeadlineUnarchived,
            AuditEntities.Deadline,
            assignment.Id.ToString(),
            oldValue: null,
            newValue: detail,
            now));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
