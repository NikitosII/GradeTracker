using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Studies.Queries.GetOwnGrades;

/// <summary>
/// A page of the caller's own grades, optionally filtered to a single subject.
/// </summary>
public sealed record GetOwnGradesQuery(
    long TelegramUserId,
    Guid? SubjectId,
    int Page = 1,
    int PageSize = 5) : IQuery<GradesPageDto>;
