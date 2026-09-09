using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Admin;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Users;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Commands.UpdateOwnGrade;

internal sealed class UpdateOwnGradeCommandHandler : ICommandHandler<UpdateOwnGradeCommand, GradeDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateOwnGradeCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<GradeDto>> Handle(UpdateOwnGradeCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<GradeDto>(UserErrors.NotBound);
        }

        var grade = await _db.Grades
            .FirstOrDefaultAsync(g => g.Id == request.GradeId, cancellationToken);

        if (grade is null)
        {
            return Result.Failure<GradeDto>(GradeErrors.NotFound);
        }

        if (grade.StudentUserId != user.Id)
        {
            return Result.Failure<GradeDto>(GradeErrors.NotOwner);
        }

        var oldValue = grade.Value;

        grade.Update(
            value: request.Value,
            weight: request.Weight,
            comment: request.Comment,
            occurredAt: request.OccurredAt,
            updatedByUserId: user.Id,
            nowUtc: _clock.UtcNow);

        var subjectName = await _db.Subjects
            .Where(s => s.Id == grade.SubjectId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        _db.AuditLogs.Add(AuditLog.Create(
            user.Id, AuditActions.GradeUpdated, AuditEntities.Grade, grade.Id.ToString(),
            oldValue: $"{subjectName}: {oldValue}", newValue: $"{subjectName}: {request.Value}", _clock.UtcNow));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(grade.ToGradeDto(subjectName));
    }
}
