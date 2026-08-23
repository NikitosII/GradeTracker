using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin;

/// <summary>
/// Resolves the calling Telegram user and confirms they hold the administrator role.
/// </summary>
internal static class AdminGuard
{
    /// <summary>
    /// Returns the tracked caller entity when they are a bound administrator
    /// </summary>
    public static async Task<Result<User>> RequireAdminAsync(IApplicationDbContext db, long telegramUserId, CancellationToken cancellationToken)
    {
        var caller = await db.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, cancellationToken);

        if (caller is null)
        {
            return Result.Failure<User>(UserErrors.NotBound);
        }

        return caller.Role != UserRole.Admin
            ? Result.Failure<User>(AdminErrors.NotAdmin)
            : Result.Success(caller);
    }
}
