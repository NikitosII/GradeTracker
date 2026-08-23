using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Queries.GetInviteCodes;

internal sealed class GetInviteCodesQueryHandler : IQueryHandler<GetInviteCodesQuery, IReadOnlyList<InviteCodeDto>>
{
    private const int MaxTake = 50;

    private readonly IApplicationDbContext _db;

    public GetInviteCodesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<InviteCodeDto>>> Handle(GetInviteCodesQuery request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<InviteCodeDto>>(gate.Error);
        }

        var take = Math.Clamp(request.Take, 1, MaxTake);

        var rows = await _db.InviteCodes
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Take(take)
            .Select(c => new { c.Id, c.Code, c.Role, c.ExpiresAt, c.UsedByUserId, c.CreatedAt })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(c => new InviteCodeDto(
                c.Id,
                c.Code,
                c.Role.ToString(),
                c.ExpiresAt,
                c.UsedByUserId is not null,
                c.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<InviteCodeDto>>(items);
    }
}
