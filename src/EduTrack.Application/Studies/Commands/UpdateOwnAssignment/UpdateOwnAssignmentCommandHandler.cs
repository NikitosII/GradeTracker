using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Users;
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

        assignment.Update(
            type: request.Type,
            title: request.Title,
            description: request.Description,
            dueAtUtc: request.DueAtUtc,
            updatedByUserId: user.Id,
            nowUtc: _clock.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);

        var subjectName = await _db.Subjects
            .Where(s => s.Id == assignment.SubjectId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return Result.Success(assignment.ToAssignmentDto(subjectName));
    }
}
