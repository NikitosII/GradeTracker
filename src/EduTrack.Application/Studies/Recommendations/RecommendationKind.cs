namespace EduTrack.Application.Studies.Recommendations;

/// <summary>The kind of advice a <see cref="RecommendationDto"/> carries, in rough urgency order.</summary>
public enum RecommendationKind
{
    OverdueDeadlines,
    UrgentDeadline,
    WeekWorkload,
    FallingAverage,
    LowSubject,
    RisingAverage,
    NoGradesYet,
}
