namespace EduTrack.Application.Admin;

/// <summary>Stable action codes written to the audit log.</summary>
public static class AuditActions
{
    public const string InviteCodeCreated = "InviteCodeCreated";
    public const string UserRoleChanged = "UserRoleChanged";
    public const string SubjectCreated = "SubjectCreated";
    public const string SubjectUpdated = "SubjectUpdated";
    public const string AnnouncementSent = "AnnouncementSent";
}

/// <summary>Entity type names written to the audit log.</summary>
public static class AuditEntities
{
    public const string InviteCode = "InviteCode";
    public const string User = "User";
    public const string Subject = "Subject";
    public const string Announcement = "Announcement";
}
