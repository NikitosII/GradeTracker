using EduTrack.Domain.Common;

namespace EduTrack.Application.Users;

public static class UserErrors
{
    public static readonly Error AlreadyBound =
        Error.Conflict("Users.AlreadyBound", "This Telegram account is already linked to a profile.");

    public static readonly Error InvalidCode =
        Error.NotFound("Users.InvalidCode", "Invite code not found.");

    public static readonly Error CodeNotUsable =
        Error.Conflict("Users.CodeNotUsable", "This invite code has expired or has already been used.");

    public static readonly Error NotBound =
        Error.NotFound("Users.NotBound", "You are not linked yet. Use /bind <code> to link your account.");
}
