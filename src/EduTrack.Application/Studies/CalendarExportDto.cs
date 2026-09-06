namespace EduTrack.Application.Studies;

/// <summary>A generated iCalendar file ready to send to the user.</summary>
public sealed record CalendarExportDto(string FileName, byte[] Content, int EventCount);
