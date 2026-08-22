namespace EduTrack.Application.Studies;

/// <summary>
/// A page of a student's deadlines.
/// </summary>
public sealed record AssignmentsPageDto(
    IReadOnlyList<AssignmentDto> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}
