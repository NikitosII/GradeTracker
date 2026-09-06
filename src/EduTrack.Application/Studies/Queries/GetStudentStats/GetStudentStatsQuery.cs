using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.Stats;

namespace EduTrack.Application.Studies.Queries.GetStudentStats;

/// <summary>Computes the caller's grade statistics snapshot.</summary>
public sealed record GetStudentStatsQuery(long TelegramUserId) : IQuery<StudentStatsDto>;
