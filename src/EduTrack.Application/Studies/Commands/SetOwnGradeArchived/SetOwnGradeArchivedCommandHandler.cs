using System.Globalization;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Admin;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Users;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Commands.SetOwnGradeArchived;

internal sealed class SetOwnGradeArchivedCommandHandler : ICommandHandler<SetOwnGradeArchivedCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SetOwnGradeArchivedCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> Handle(SetOwnGradeArchivedCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotBound);
        }

        // Bypass the global filter so an already-archived grade can be found and restored.
        var grade = await _db.Grades
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == request.GradeId && !g.IsDeleted, cancellationToken);

        if (grade is null)
        {
            return Result.Failure(GradeErrors.NotFound);
        }

        if (grade.StudentUserId != user.Id)
        {
            return Result.Failure(GradeErrors.NotOwner);
        }

        if (grade.IsArchived == request.Archived)
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        var detail = $"{grade.Value.ToString(CultureInfo.InvariantCulture)} · {grade.OccurredAt:yyyy-MM-dd}";

        if (request.Archived)
        {
            grade.Archive(user.Id, now);
        }
        else
        {
            grade.Unarchive(user.Id, now);
        }

        _db.AuditLogs.Add(AuditLog.Create(
            user.Id,
            request.Archived ? AuditActions.GradeArchived : AuditActions.GradeUnarchived,
            AuditEntities.Grade,
            grade.Id.ToString(),
            oldValue: null,
            newValue: detail,
            now));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
