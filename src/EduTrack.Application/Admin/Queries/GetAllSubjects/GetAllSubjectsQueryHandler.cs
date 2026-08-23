using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Queries.GetAllSubjects;

internal sealed class GetAllSubjectsQueryHandler : IQueryHandler<GetAllSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    private readonly IApplicationDbContext _db;

    public GetAllSubjectsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<SubjectDto>>> Handle(GetAllSubjectsQuery request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SubjectDto>>(gate.Error);
        }

        var subjects = await _db.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SubjectDto>>(subjects);
    }
}
