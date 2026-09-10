namespace EduTrack.Application.Studies.Recommendations;

/// <summary>
/// One piece of advice for the student. Only the fields relevant to <see cref="Kind"/> are set;
/// the presentation layer maps the kind to a localized message and formats the fields.
/// </summary>
public sealed record RecommendationDto(
    RecommendationKind Kind,
    string? SubjectName,
    int Count,
    double? Value,
    double? PreviousValue,
    string? Title,
    DateTime? DueAtUtc);

/// <summary>The student's ranked recommendations for the /tips command.</summary>
public sealed record StudentRecommendationsDto(IReadOnlyList<RecommendationDto> Items);
