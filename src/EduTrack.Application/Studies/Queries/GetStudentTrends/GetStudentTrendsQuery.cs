using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.Stats;

namespace EduTrack.Application.Studies.Queries.GetStudentTrends;

/// <summary>Computes the caller's per-subject grade dynamics.</summary>
public sealed record GetStudentTrendsQuery(long TelegramUserId) : IQuery<StudentTrendsDto>;
