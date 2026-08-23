using EduTrack.Domain.Common;

namespace EduTrack.Application.Admin;

public static class AdminErrors
{
    public static readonly Error NotAdmin =
        Error.AccessDenied("Admin.NotAdmin", "This command is only available to administrators.");

    public static readonly Error UserNotFound =
        Error.NotFound("Admin.UserNotFound", "User not found.");

    public static readonly Error SubjectNotFound =
        Error.NotFound("Admin.SubjectNotFound", "Subject not found.");

    public static readonly Error SubjectNameTaken =
        Error.Conflict("Admin.SubjectNameTaken", "A subject with this name already exists.");

    public static readonly Error CannotDemoteSelf =
        Error.Conflict("Admin.CannotDemoteSelf", "You cannot change your own role.");
}
