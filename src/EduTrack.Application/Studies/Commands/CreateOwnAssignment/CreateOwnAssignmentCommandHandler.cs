using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Reminders;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using EduTrack.Domain.Studies;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Commands.CreateOwnAssignment;

internal sealed class CreateOwnAssignmentCommandHandler : ICommandHandler<CreateOwnAssignmentCommand, AssignmentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateOwnAssignmentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<AssignmentDto>> Handle(CreateOwnAssignmentCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AssignmentDto>(UserErrors.NotBound);
        }

        var subject = await _db.Subjects
            .FirstOrDefaultAsync(s => s.Id == request.SubjectId && s.IsActive, cancellationToken);

        if (subject is null)
        {
            return Result.Failure<AssignmentDto>(AssignmentErrors.SubjectNotFound);
        }

        var now = _clock.UtcNow;

        var assignment = Assignment.Create(
            ownerUserId: user.Id,
            subjectId: subject.Id,
            type: request.Type,
            title: request.Title,
            description: request.Description,
            dueAtUtc: request.DueAtUtc,
            createdByUserId: user.Id,
            nowUtc: now);

        _db.Assignments.Add(assignment);
        await ReminderPlanner.SyncAsync(_db, assignment, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(assignment.ToAssignmentDto(subject.Name));
    }
}
