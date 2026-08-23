namespace EduTrack.Application.Admin;

public sealed record SystemStatusDto(
    int TotalUsers,
    int Admins,
    int Students,
    int Subjects,
    int ActiveSubjects,
    int Grades,
    int Assignments,
    int InviteCodes,
    int UnusedInviteCodes,
    int AuditEntries,
    DateTime GeneratedAtUtc);
