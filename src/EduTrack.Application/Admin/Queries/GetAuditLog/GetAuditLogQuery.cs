using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Admin.Queries.GetAuditLog;

/// <summary>Lists audit-log entries for an administrator, newest first.</summary>
public sealed record GetAuditLogQuery(
    long CallerTelegramUserId,
    int Page = 1,
    int PageSize = 8) : IQuery<AuditLogPageDto>;
