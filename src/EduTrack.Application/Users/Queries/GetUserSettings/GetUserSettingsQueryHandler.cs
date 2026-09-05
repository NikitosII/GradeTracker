using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Users.Queries.GetUserSettings;

internal sealed class GetUserSettingsQueryHandler : IQueryHandler<GetUserSettingsQuery, UserSettingsDto>
{
    private readonly IApplicationDbContext _db;

    public GetUserSettingsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<UserSettingsDto>> Handle(GetUserSettingsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        return user is null
            ? Result.Failure<UserSettingsDto>(UserErrors.NotBound)
            : Result.Success(user.ToSettingsDto());
    }
}
