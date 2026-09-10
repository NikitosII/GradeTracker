using System.Globalization;
using System.Text;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Localization;
using EduTrack.Application.Studies.Recommendations;
using EduTrack.Application.Studies.Queries.GetStudentRecommendations;
using EduTrack.Bot.Web.Localization;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>Handles /tips: a short, ranked list of actionable recommendations for the student.</summary>
public sealed class RecommendationsModule
{
    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly IUiText _text;
    private readonly ILogger<RecommendationsModule> _logger;

    public RecommendationsModule(ISender sender, ITelegramSender telegram, IUiText text, ILogger<RecommendationsModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _text = text;
        _logger = logger;
    }

    public async Task ShowTipsAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetStudentRecommendationsQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        await _telegram.SendTextAsync(chatId, Render(result.Value), ct);
    }

    private string Render(StudentRecommendationsDto tips)
    {
        var title = _text.Get(TextKeys.TipsTitle);
        if (tips.Items.Count == 0)
        {
            return $"{title}\n\n{_text.Get(TextKeys.TipsEmpty)}";
        }

        var sb = new StringBuilder();
        sb.Append(title).Append("\n\n");
        var index = 1;
        foreach (var tip in tips.Items)
        {
            sb.Append(index).Append(". ").Append(Format(tip)).Append('\n');
            index++;
        }

        return sb.ToString().TrimEnd();
    }

    private string Format(RecommendationDto tip) => tip.Kind switch
    {
        RecommendationKind.OverdueDeadlines => _text.Get(TextKeys.RecOverdue, tip.Count),
        RecommendationKind.UrgentDeadline => _text.Get(
            TextKeys.RecUrgent, tip.Title ?? string.Empty, tip.SubjectName ?? string.Empty, $"{tip.DueAtUtc:yyyy-MM-dd HH:mm} UTC"),
        RecommendationKind.WeekWorkload => _text.Get(TextKeys.RecWeekWorkload, tip.Count),
        RecommendationKind.FallingAverage => _text.Get(
            TextKeys.RecFalling, tip.SubjectName ?? string.Empty, Avg(tip.PreviousValue), Avg(tip.Value)),
        RecommendationKind.LowSubject => _text.Get(TextKeys.RecLowSubject, tip.SubjectName ?? string.Empty, Avg(tip.Value)),
        RecommendationKind.RisingAverage => _text.Get(
            TextKeys.RecRising, tip.SubjectName ?? string.Empty, Avg(tip.PreviousValue), Avg(tip.Value)),
        RecommendationKind.NoGradesYet => _text.Get(TextKeys.RecNoGrades),
        _ => string.Empty,
    };

    private static string Avg(double? value) => value?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—";
}
