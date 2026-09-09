using System.Text;
using EduTrack.Application.Admin;
using EduTrack.Application.Localization;
using EduTrack.Application.Studies.History;
using EduTrack.Application.Studies.Queries.GetOwnHistory;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Bot.Web.Localization;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>Handles /history: the student's own change log, paged.</summary>
public sealed class HistoryModule
{
    public const string Namespace = CallbackData.HistoryNamespace;

    private const int PageSize = 8;

    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly IUiText _text;
    private readonly ILogger<HistoryModule> _logger;

    public HistoryModule(ISender sender, ITelegramSender telegram, IUiText text, ILogger<HistoryModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _text = text;
        _logger = logger;
    }

    public async Task ShowHistoryAsync(long chatId, long telegramUserId, int page, CancellationToken ct)
    {
        var result = await _sender.Send(new GetOwnHistoryQuery(telegramUserId, page, PageSize), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        await _telegram.SendKeyboardAsync(chatId, Render(result.Value), NavRows(result.Value), ct);
    }

    public async Task HandleCallbackAsync(long chatId, long telegramUserId, string callbackQueryId, string data, CancellationToken ct)
    {
        try
        {
            var parts = CallbackData.Parts(data);
            var page = parts.Length > 1 && int.TryParse(parts[1], out var p) && p > 0 ? p : 1;
            await ShowHistoryAsync(chatId, telegramUserId, page, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle history callback {Data} from {TelegramUserId}", data, telegramUserId);
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonSomethingWrong), ct);
        }
        finally
        {
            await _telegram.AnswerCallbackAsync(callbackQueryId, cancellationToken: ct);
        }
    }

    private string Render(StudentHistoryPageDto page)
    {
        var title = _text.Get(TextKeys.HistoryTitle);

        if (page.Items.Count == 0)
        {
            return $"{title}\n\n{_text.Get(TextKeys.HistoryEmpty)}";
        }

        var sb = new StringBuilder();
        sb.Append(title).Append("\n\n");
        foreach (var entry in page.Items)
        {
            sb.Append($"{entry.CreatedAt:yyyy-MM-dd HH:mm} · {ActionLabel(entry.Action)}");
            if (!string.IsNullOrWhiteSpace(entry.Detail))
            {
                sb.Append($"\n   {entry.Detail}");
            }

            sb.Append('\n');
        }

        sb.Append('\n').Append(_text.Get(TextKeys.CommonPage, page.Page, page.TotalPages));
        return sb.ToString();
    }

    private string ActionLabel(string action) => action switch
    {
        AuditActions.GradeAdded => _text.Get(TextKeys.HistoryGradeAdded),
        AuditActions.GradeUpdated => _text.Get(TextKeys.HistoryGradeUpdated),
        AuditActions.DeadlineCreated => _text.Get(TextKeys.HistoryDeadlineCreated),
        AuditActions.DeadlineUpdated => _text.Get(TextKeys.HistoryDeadlineUpdated),
        _ => action,
    };

    private List<IReadOnlyList<InlineButton>> NavRows(StudentHistoryPageDto page)
    {
        var nav = new List<InlineButton>();
        if (page.HasPrevious)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonPrev), CallbackData.HistoryPage(page.Page - 1)));
        }

        if (page.HasNext)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonNext), CallbackData.HistoryPage(page.Page + 1)));
        }

        return nav.Count > 0
            ? new List<IReadOnlyList<InlineButton>> { nav }
            : new List<IReadOnlyList<InlineButton>>();
    }
}
