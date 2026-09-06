using System.Globalization;
using System.Text;
using EduTrack.Application.Studies.Queries.GetStudentStats;
using EduTrack.Application.Studies.Stats;
using EduTrack.Application.Abstractions.Telegram;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>Handles /stats: a text snapshot of the caller's performance.</summary>
public sealed class StatsModule
{
    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;

    public StatsModule(ISender sender, ITelegramSender telegram)
    {
        _sender = sender;
        _telegram = telegram;
    }

    public async Task ShowStatsAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetStudentStatsQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return;
        }

        await _telegram.SendTextAsync(chatId, Render(result.Value), ct);
    }

    private static string Render(StudentStatsDto s)
    {
        if (s.TotalGrades == 0)
        {
            return " Your stats\n\nNo grades yet. Add one with /grade_add.";
        }

        var sb = new StringBuilder();
        sb.Append(" Your stats\n\n");
        sb.Append(CultureInfo.InvariantCulture, $"GPA (all time): {Avg(s.OverallGpa)}\n");
        sb.Append(CultureInfo.InvariantCulture, $"This week: {Avg(s.WeekAverage)} ({s.WeekCount} grade(s))\n");
        sb.Append(CultureInfo.InvariantCulture, $"This month: {Avg(s.MonthAverage)} ({s.MonthCount} grade(s))\n");

        if (s.Subjects.Count > 0)
        {
            sb.Append("\nBy subject:\n");
            foreach (var subject in s.Subjects)
            {
                sb.Append(CultureInfo.InvariantCulture, $"• {subject.SubjectName}: {subject.Average:0.00} ({subject.Count})\n");
            }
        }

        if (s.WorstSubject is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\nNeeds attention: {s.WorstSubject} ({Avg(s.WorstSubjectAverage)})\n");
        }

        sb.Append(CultureInfo.InvariantCulture, $"Upcoming deadlines: {s.UpcomingDeadlines}\n");
        sb.Append(CultureInfo.InvariantCulture, $"Grades recorded: {s.TotalGrades}");

        return sb.ToString();
    }

    private static string Avg(double? value) =>
        value is { } v ? v.ToString("0.00", CultureInfo.InvariantCulture) : "—";
}
