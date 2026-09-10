using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies.Recommendations;

namespace EduTrack.Application.Studies.Queries.GetStudentRecommendations;

/// <summary>Returns a short, ranked list of actionable tips for the caller.</summary>
public sealed record GetStudentRecommendationsQuery(long TelegramUserId) : IQuery<StudentRecommendationsDto>;
