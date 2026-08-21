using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Studies.Queries.GetSubjects;

/// <summary>Active subjects a student can pick when recording grades or deadlines.</summary>
public sealed record GetSubjectsQuery : IQuery<IReadOnlyList<SubjectDto>>;
