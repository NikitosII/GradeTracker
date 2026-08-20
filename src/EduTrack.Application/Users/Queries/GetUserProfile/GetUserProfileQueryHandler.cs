using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Users.Queries.GetUserProfile;

internal sealed class GetUserProfileQueryHandler : IQueryHandler<GetUserProfileQuery, UserProfileDto>
{
    private readonly IApplicationDbContext _db;

    public GetUserProfileQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        return user is null
            ? Result.Failure<UserProfileDto>(UserErrors.NotBound)
            : Result.Success(user.ToProfileDto());
    }
}
