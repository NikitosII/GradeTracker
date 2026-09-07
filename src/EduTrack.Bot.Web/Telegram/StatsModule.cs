using System.Globalization;
using System.Text;
using EduTrack.Application.Localization;
using EduTrack.Application.Studies.Queries.GetStudentStats;
using EduTrack.Application.Studies.Stats;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Bot.Web.Localization;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>Handles /stats: a text snapshot of the caller's performance.</summary>
public sealed class StatsModule
{
    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly IUiText _text;

    public StatsModule(ISender sender, ITelegramSender telegram, IUiText text)
    {
        _sender = sender;
        _telegram = telegram;
        _text = text;
    }

    public async Task ShowStatsAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetStudentStatsQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        await _telegram.SendTextAsync(chatId, Render(result.Value), ct);
    }

    private string Render(StudentStatsDto s)
    {
        var title = _text.Get(TextKeys.StatsTitle);

        if (s.TotalGrades == 0)
        {
            return $"{title}\n\n{_text.Get(TextKeys.StatsNoGrades)}";
        }

        var sb = new StringBuilder();
        sb.Append(title).Append("\n\n");
        sb.Append(_text.Get(TextKeys.StatsGpa, Avg(s.OverallGpa))).Append('\n');
        sb.Append(_text.Get(TextKeys.StatsWeek, Avg(s.WeekAverage), s.WeekCount)).Append('\n');
        sb.Append(_text.Get(TextKeys.StatsMonth, Avg(s.MonthAverage), s.MonthCount)).Append('\n');

        if (s.Subjects.Count > 0)
        {
            sb.Append('\n').Append(_text.Get(TextKeys.StatsBySubject)).Append('\n');
            foreach (var subject in s.Subjects)
            {
                sb.Append(_text.Get(TextKeys.StatsSubjectLine, subject.SubjectName, Number(subject.Average), subject.Count)).Append('\n');
            }
        }

        if (s.WorstSubject is not null)
        {
            sb.Append('\n').Append(_text.Get(TextKeys.StatsNeedsAttention, s.WorstSubject, Avg(s.WorstSubjectAverage))).Append('\n');
        }

        sb.Append(_text.Get(TextKeys.StatsUpcoming, s.UpcomingDeadlines)).Append('\n');
        sb.Append(_text.Get(TextKeys.StatsRecorded, s.TotalGrades));

        return sb.ToString();
    }

    private static string Avg(double? value) => value is { } v ? Number(v) : "—";

    private static string Number(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
