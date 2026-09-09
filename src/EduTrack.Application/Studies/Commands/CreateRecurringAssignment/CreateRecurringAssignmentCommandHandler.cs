using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Admin;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Reminders;
using EduTrack.Application.Studies.Recurrence;
using EduTrack.Application.Users;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using EduTrack.Domain.Studies;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Commands.CreateRecurringAssignment;

internal sealed class CreateRecurringAssignmentCommandHandler : ICommandHandler<CreateRecurringAssignmentCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateRecurringAssignmentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(CreateRecurringAssignmentCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<int>(UserErrors.NotBound);
        }

        var subject = await _db.Subjects
            .FirstOrDefaultAsync(s => s.Id == request.SubjectId && s.IsActive, cancellationToken);

        if (subject is null)
        {
            return Result.Failure<int>(AssignmentErrors.SubjectNotFound);
        }

        var now = _clock.UtcNow;
        var occurrences = RecurrenceSchedule.Occurrences(
            request.FirstDueAtUtc, request.Frequency, request.Interval, request.Count);

        foreach (var dueAtUtc in occurrences)
        {
            var assignment = Assignment.Create(
                ownerUserId: user.Id,
                subjectId: subject.Id,
                type: request.Type,
                title: request.Title,
                description: request.Description,
                dueAtUtc: dueAtUtc,
                createdByUserId: user.Id,
                nowUtc: now);

            _db.Assignments.Add(assignment);
            await ReminderPlanner.SyncAsync(_db, assignment, now, cancellationToken);
        }

        _db.AuditLogs.Add(AuditLog.Create(
            user.Id, AuditActions.DeadlineCreated, AuditEntities.Deadline, null,
            oldValue: null, newValue: $"{request.Title} ×{occurrences.Count} ({request.Frequency})", now));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(occurrences.Count);
    }
}
