using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Queries.GetUsers;

internal sealed class GetUsersQueryHandler : IQueryHandler<GetUsersQuery, AdminUsersPageDto>
{
    private const int MaxPageSize = 20;

    private readonly IApplicationDbContext _db;

    public GetUsersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<AdminUsersPageDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<AdminUsersPageDto>(gate.Error);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var totalCount = await _db.Users.AsNoTracking().CountAsync(cancellationToken);

        var rows = await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.TelegramUserId, u.Username, u.FirstName, u.LastName, u.Role, u.CreatedAt })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(u => new AdminUserDto(
                u.Id,
                u.TelegramUserId,
                u.Username,
                BuildFullName(u.FirstName, u.LastName),
                u.Role.ToString(),
                u.CreatedAt))
            .ToList();

        return Result.Success(new AdminUsersPageDto(items, page, pageSize, totalCount));
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        var name = string.Join(' ', new[] { firstName, lastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
