using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Studies.Queries.GetSubjects;

internal sealed class GetSubjectsQueryHandler : IQueryHandler<GetSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSubjectsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<SubjectDto>>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        var subjects = await _db.Subjects
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SubjectDto>>(subjects);
    }
}
