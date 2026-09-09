namespace EduTrack.Application.Studies.Stats;

/// <summary>A subject's weighted average this period vs. the previous one.</summary>
public sealed record SubjectTrendDto(
    string SubjectName,
    double? CurrentAverage,
    double? PreviousAverage,
    double? Delta,
    int CurrentCount);

/// <summary>Per-subject dynamics for the /trends command</summary>
public sealed record StudentTrendsDto(IReadOnlyList<SubjectTrendDto> Subjects);
