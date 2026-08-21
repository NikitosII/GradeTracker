namespace EduTrack.Application.Studies;

/// <summary>
/// A page of a student's grades.
/// </summary>
public sealed record GradesPageDto(
    Guid? SubjectId,
    IReadOnlyList<GradeDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    double? Average)
{
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}
