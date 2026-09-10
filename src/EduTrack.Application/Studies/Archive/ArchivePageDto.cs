namespace EduTrack.Application.Studies.Archive;

/// <summary>One archivable/archived item (a grade or a deadline) for the /archive views.</summary>
public sealed record ArchiveItemDto(Guid Id, string Primary, string Secondary);

/// <summary>A page of archivable/archived items, newest first.</summary>
public sealed record ArchivePageDto(
    IReadOnlyList<ArchiveItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
