namespace EduTrack.Application.Studies.Stats;

/// <summary>Weighted average and grade count for one subject.</summary>
public sealed record SubjectStatDto(string SubjectName, double Average, int Count);

/// <summary>A snapshot of a student's performance for the /stats command.</summary>
public sealed record StudentStatsDto(
    double? OverallGpa,
    double? WeekAverage,
    int WeekCount,
    double? MonthAverage,
    int MonthCount,
    IReadOnlyList<SubjectStatDto> Subjects,
    string? WorstSubject,
    double? WorstSubjectAverage,
    int UpcomingDeadlines,
    int TotalGrades);
