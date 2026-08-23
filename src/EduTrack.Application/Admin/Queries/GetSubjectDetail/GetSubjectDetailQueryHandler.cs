using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Queries.GetSubjectDetail;

internal sealed class GetSubjectDetailQueryHandler : IQueryHandler<GetSubjectDetailQuery, AdminSubjectDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetSubjectDetailQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<AdminSubjectDetailDto>> Handle(GetSubjectDetailQuery request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<AdminSubjectDetailDto>(gate.Error);
        }

        var subject = await _db.Subjects
            .AsNoTracking()
            .Where(s => s.Id == request.SubjectId)
            .Select(s => new AdminSubjectDetailDto(s.Id, s.Name, s.Description, s.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return subject is null
            ? Result.Failure<AdminSubjectDetailDto>(AdminErrors.SubjectNotFound)
            : Result.Success(subject);
    }
}
