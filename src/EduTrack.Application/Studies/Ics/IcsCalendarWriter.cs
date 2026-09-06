using System.Globalization;
using System.Text;

namespace EduTrack.Application.Studies.Ics;

/// <summary>A single calendar event to serialise into an iCalendar VEVENT.</summary>
public sealed record IcsEvent(string Uid, DateTime StartUtc, string Summary, string? Description);

public static class IcsCalendarWriter
{
    private const string ProdId = "-//EduTrack//GradeTracker//EN";
    private const int MaxLineOctets = 75;

    public static string Write(IReadOnlyList<IcsEvent> events, DateTime nowUtc)
    {
        var sb = new StringBuilder();
        var stamp = FormatUtc(nowUtc);

        Fold(sb, "BEGIN:VCALENDAR");
        Fold(sb, "VERSION:2.0");
        Fold(sb, $"PRODID:{ProdId}");
        Fold(sb, "CALSCALE:GREGORIAN");
        Fold(sb, "METHOD:PUBLISH");

        foreach (var e in events)
        {
            Fold(sb, "BEGIN:VEVENT");
            Fold(sb, $"UID:{e.Uid}");
            Fold(sb, $"DTSTAMP:{stamp}");
            Fold(sb, $"DTSTART:{FormatUtc(e.StartUtc)}");
            Fold(sb, $"SUMMARY:{Escape(e.Summary)}");
            if (!string.IsNullOrWhiteSpace(e.Description))
            {
                Fold(sb, $"DESCRIPTION:{Escape(e.Description!)}");
            }

            // Mirror the bot's 24h-ahead reminder as a calendar alarm.
            Fold(sb, "BEGIN:VALARM");
            Fold(sb, "ACTION:DISPLAY");
            Fold(sb, "TRIGGER:-P1D");
            Fold(sb, $"DESCRIPTION:{Escape(e.Summary)}");
            Fold(sb, "END:VALARM");

            Fold(sb, "END:VEVENT");
        }

        Fold(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    private static string FormatUtc(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string text) => text
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n");

    private static void Fold(StringBuilder sb, string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        var pos = 0;
        var first = true;

        while (pos < bytes.Length)
        {
            var limit = first ? MaxLineOctets : MaxLineOctets - 1; // continuation carries a leading space
            var remaining = bytes.Length - pos;
            var take = Math.Min(limit, remaining);

            if (take < remaining)
            {
                // Back off so we don't slice through a multi-byte UTF-8 sequence.
                while (take > 0 && (bytes[pos + take] & 0xC0) == 0x80)
                {
                    take--;
                }
            }

            if (!first)
            {
                sb.Append(' ');
            }

            sb.Append(Encoding.UTF8.GetString(bytes, pos, take));
            sb.Append("\r\n");

            pos += take;
            first = false;
        }
    }
}
