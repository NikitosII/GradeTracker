using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Admin;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Users;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using EduTrack.Domain.Studies;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Commands.AddOwnGrade;

internal sealed class AddOwnGradeCommandHandler : ICommandHandler<AddOwnGradeCommand, GradeDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AddOwnGradeCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<GradeDto>> Handle(AddOwnGradeCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<GradeDto>(UserErrors.NotBound);
        }

        var subject = await _db.Subjects
            .FirstOrDefaultAsync(s => s.Id == request.SubjectId && s.IsActive, cancellationToken);

        if (subject is null)
        {
            return Result.Failure<GradeDto>(GradeErrors.SubjectNotFound);
        }

        var grade = Grade.Add(
            studentUserId: user.Id,
            subjectId: subject.Id,
            value: request.Value,
            weight: request.Weight,
            comment: request.Comment,
            occurredAt: request.OccurredAt,
            createdByUserId: user.Id,
            nowUtc: _clock.UtcNow);

        _db.Grades.Add(grade);
        _db.AuditLogs.Add(AuditLog.Create(
            user.Id, AuditActions.GradeAdded, AuditEntities.Grade, grade.Id.ToString(),
            oldValue: null, newValue: $"{subject.Name}: {request.Value}", _clock.UtcNow));
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(grade.ToGradeDto(subject.Name));
    }
}
