using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies;

namespace EduTrack.Application.Admin.Queries.GetAllSubjects;

/// <summary>Lists every subject (active and inactive) for administrative management.</summary>
public sealed record GetAllSubjectsQuery(long CallerTelegramUserId) : IQuery<IReadOnlyList<SubjectDto>>;
