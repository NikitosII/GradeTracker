using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Domain.Common;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Users.Commands.BindUser;

internal sealed class BindUserCommandHandler : ICommandHandler<BindUserCommand, UserProfileDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public BindUserCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<UserProfileDto>> Handle(BindUserCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var alreadyBound = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (alreadyBound)
        {
            return Result.Failure<UserProfileDto>(UserErrors.AlreadyBound);
        }

        var invite = await _db.InviteCodes
            .FirstOrDefaultAsync(c => c.Code == request.Code, cancellationToken);

        if (invite is null)
        {
            return Result.Failure<UserProfileDto>(UserErrors.InvalidCode);
        }

        if (!invite.CanBeRedeemed(now))
        {
            return Result.Failure<UserProfileDto>(UserErrors.CodeNotUsable);
        }

        var user = User.Register(
            request.TelegramUserId,
            request.Username,
            request.FirstName,
            request.LastName,
            invite.Role,
            now);

        invite.Redeem(user.Id);
        _db.Users.Add(user);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(user.ToProfileDto());
    }
}
