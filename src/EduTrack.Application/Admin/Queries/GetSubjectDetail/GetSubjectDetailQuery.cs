using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Admin.Queries.GetSubjectDetail;

/// <summary>Full detail for a single subject, used before an administrative edit.</summary>
public sealed record GetSubjectDetailQuery(
    long CallerTelegramUserId,
    Guid SubjectId) : IQuery<AdminSubjectDetailDto>;
