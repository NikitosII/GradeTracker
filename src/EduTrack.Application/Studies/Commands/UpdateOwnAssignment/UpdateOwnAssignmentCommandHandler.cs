using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Admin;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Reminders;
using EduTrack.Application.Users;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Commands.UpdateOwnAssignment;

internal sealed class UpdateOwnAssignmentCommandHandler : ICommandHandler<UpdateOwnAssignmentCommand, AssignmentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateOwnAssignmentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<AssignmentDto>> Handle(UpdateOwnAssignmentCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AssignmentDto>(UserErrors.NotBound);
        }

        var assignment = await _db.Assignments
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

        if (assignment is null)
        {
            return Result.Failure<AssignmentDto>(AssignmentErrors.NotFound);
        }

        if (assignment.OwnerUserId != user.Id)
        {
            return Result.Failure<AssignmentDto>(AssignmentErrors.NotOwner);
        }

        var now = _clock.UtcNow;
        var oldValue = $"{assignment.Title} — {assignment.DueAtUtc:yyyy-MM-dd HH:mm} UTC";

        assignment.Update(
            type: request.Type,
            title: request.Title,
            description: request.Description,
            dueAtUtc: request.DueAtUtc,
            updatedByUserId: user.Id,
            nowUtc: now);

        _db.AuditLogs.Add(AuditLog.Create(
            user.Id, AuditActions.DeadlineUpdated, AuditEntities.Deadline, assignment.Id.ToString(),
            oldValue: oldValue, newValue: $"{request.Title} — {request.DueAtUtc:yyyy-MM-dd HH:mm} UTC", now));

        await ReminderPlanner.SyncAsync(_db, assignment, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var subjectName = await _db.Subjects
            .Where(s => s.Id == assignment.SubjectId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return Result.Success(assignment.ToAssignmentDto(subjectName));
    }
}
