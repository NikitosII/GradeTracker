namespace EduTrack.Application.Studies.History;

/// <summary>One change the student made, for the /history view.</summary>
public sealed record StudentHistoryEntryDto(string Action, string EntityType, string? Detail, DateTime CreatedAt);

/// <summary>A page of the caller's own recorded changes, newest first.</summary>
public sealed record StudentHistoryPageDto(
    IReadOnlyList<StudentHistoryEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
