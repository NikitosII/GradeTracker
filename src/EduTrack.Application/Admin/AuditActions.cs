namespace EduTrack.Application.Admin;

/// <summary>Stable action codes written to the audit log.</summary>
public static class AuditActions
{
    public const string InviteCodeCreated = "InviteCodeCreated";
    public const string UserRoleChanged = "UserRoleChanged";
    public const string SubjectCreated = "SubjectCreated";
    public const string SubjectUpdated = "SubjectUpdated";
    public const string AnnouncementSent = "AnnouncementSent";
    public const string SettingsUpdated = "SettingsUpdated";
    public const string GradeAdded = "GradeAdded";
    public const string GradeUpdated = "GradeUpdated";
    public const string DeadlineCreated = "DeadlineCreated";
    public const string DeadlineUpdated = "DeadlineUpdated";
    public const string GradeArchived = "GradeArchived";
    public const string GradeUnarchived = "GradeUnarchived";
    public const string DeadlineArchived = "DeadlineArchived";
    public const string DeadlineUnarchived = "DeadlineUnarchived";
}

/// <summary>Entity type names written to the audit log.</summary>
public static class AuditEntities
{
    public const string InviteCode = "InviteCode";
    public const string User = "User";
    public const string Subject = "Subject";
    public const string Announcement = "Announcement";
    public const string Grade = "Grade";
    public const string Deadline = "Deadline";
}
