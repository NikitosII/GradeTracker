using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Studies.Queries.ExportCalendar;

/// <summary>Builds an iCalendar (.ics) file of the caller's own deadlines.</summary>
public sealed record ExportCalendarQuery(long TelegramUserId) : IQuery<CalendarExportDto>;
