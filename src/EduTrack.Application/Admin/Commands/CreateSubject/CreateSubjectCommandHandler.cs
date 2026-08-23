using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Studies;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using EduTrack.Domain.Studies;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Commands.CreateSubject;

internal sealed class CreateSubjectCommandHandler : ICommandHandler<CreateSubjectCommand, SubjectDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateSubjectCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<SubjectDto>> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<SubjectDto>(gate.Error);
        }

        var name = request.Name.Trim();
        var nameTaken = await _db.Subjects
            .AsNoTracking()
            .AnyAsync(s => s.Name == name, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<SubjectDto>(AdminErrors.SubjectNameTaken);
        }

        var now = _clock.UtcNow;
        var subject = Subject.Create(name, string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim(), now);
        _db.Subjects.Add(subject);

        _db.AuditLogs.Add(AuditLog.Create(
            gate.Value.Id,
            AuditActions.SubjectCreated,
            AuditEntities.Subject,
            subject.Id.ToString(),
            oldValue: null,
            newValue: subject.Name,
            now));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubjectDto(subject.Id, subject.Name, subject.IsActive));
    }
}
