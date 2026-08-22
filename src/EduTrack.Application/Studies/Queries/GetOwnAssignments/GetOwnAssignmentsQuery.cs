using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Studies.Queries.GetOwnAssignments;

/// <summary>
/// A page of the caller's own deadlines, ordered by due date (nearest first).
/// </summary>
public sealed record GetOwnAssignmentsQuery(
    long TelegramUserId,
    Guid? SubjectId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 5) : IQuery<AssignmentsPageDto>;
