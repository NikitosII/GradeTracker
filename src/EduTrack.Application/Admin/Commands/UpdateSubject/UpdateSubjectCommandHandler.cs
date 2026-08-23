using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Studies;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Commands.UpdateSubject;

internal sealed class UpdateSubjectCommandHandler : ICommandHandler<UpdateSubjectCommand, SubjectDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateSubjectCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<SubjectDto>> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<SubjectDto>(gate.Error);
        }

        var subject = await _db.Subjects
            .FirstOrDefaultAsync(s => s.Id == request.SubjectId, cancellationToken);

        if (subject is null)
        {
            return Result.Failure<SubjectDto>(AdminErrors.SubjectNotFound);
        }

        var name = request.Name.Trim();
        var nameTaken = await _db.Subjects
            .AsNoTracking()
            .AnyAsync(s => s.Name == name && s.Id != subject.Id, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<SubjectDto>(AdminErrors.SubjectNameTaken);
        }

        var before = $"{subject.Name} (active={subject.IsActive})";

        subject.Update(name, string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim());
        if (request.IsActive)
        {
            subject.Activate();
        }
        else
        {
            subject.Deactivate();
        }

        var now = _clock.UtcNow;
        _db.AuditLogs.Add(AuditLog.Create(
            gate.Value.Id,
            AuditActions.SubjectUpdated,
            AuditEntities.Subject,
            subject.Id.ToString(),
            oldValue: before,
            newValue: $"{subject.Name} (active={subject.IsActive})",
            now));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubjectDto(subject.Id, subject.Name, subject.IsActive));
    }
}
