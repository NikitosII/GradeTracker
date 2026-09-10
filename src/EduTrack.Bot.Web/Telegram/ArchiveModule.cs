using System.Text;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Localization;
using EduTrack.Application.Studies.Archive;
using EduTrack.Application.Studies.Commands.SetOwnAssignmentArchived;
using EduTrack.Application.Studies.Commands.SetOwnGradeArchived;
using EduTrack.Application.Studies.Queries.GetOwnDeadlinesForArchive;
using EduTrack.Application.Studies.Queries.GetOwnGradesForArchive;
using EduTrack.Bot.Web.Localization;
using EduTrack.Domain.Common;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Handles /archive: hide (archive) or restore the student's own grades and deadlines.
/// </summary>
public sealed class ArchiveModule
{
    public const string Namespace = CallbackData.ArchiveNamespace;

    private const int PageSize = 8;

    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly IUiText _text;
    private readonly ILogger<ArchiveModule> _logger;

    public ArchiveModule(ISender sender, ITelegramSender telegram, IUiText text, ILogger<ArchiveModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _text = text;
        _logger = logger;
    }

    public async Task ShowMenuAsync(long chatId, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[] { new InlineButton(_text.Get(TextKeys.ArchiveBtnArchiveDeadline), CallbackData.ArchiveList(CallbackData.ArchiveDeadlinesLive, 1)) },
            new[] { new InlineButton(_text.Get(TextKeys.ArchiveBtnArchiveGrade), CallbackData.ArchiveList(CallbackData.ArchiveGradesLive, 1)) },
            new[] { new InlineButton(_text.Get(TextKeys.ArchiveBtnDeadlinesArchived), CallbackData.ArchiveList(CallbackData.ArchiveDeadlinesArchived, 1)) },
            new[] { new InlineButton(_text.Get(TextKeys.ArchiveBtnGradesArchived), CallbackData.ArchiveList(CallbackData.ArchiveGradesArchived, 1)) },
        };

        var body = $"{_text.Get(TextKeys.ArchiveTitle)}\n\n{_text.Get(TextKeys.ArchiveMenuPrompt)}";
        await _telegram.SendKeyboardAsync(chatId, body, rows, ct);
    }

    public async Task HandleCallbackAsync(long chatId, long telegramUserId, string callbackQueryId, string data, CancellationToken ct)
    {
        try
        {
            var parts = CallbackData.Parts(data);
            var action = parts.Length > 1 ? parts[1] : string.Empty;

            switch (action)
            {
                case "menu":
                    await ShowMenuAsync(chatId, ct);
                    break;

                case "list" when parts.Length >= 4:
                    await ShowListAsync(chatId, telegramUserId, parts[2], ParsePage(parts[3]), ct);
                    break;

                case "ar" when parts.Length >= 4 && Guid.TryParse(parts[3], out var archiveId):
                    await SetArchivedAsync(chatId, telegramUserId, parts[2], archiveId, archived: true, ct);
                    break;

                case "re" when parts.Length >= 4 && Guid.TryParse(parts[3], out var restoreId):
                    await SetArchivedAsync(chatId, telegramUserId, parts[2], restoreId, archived: false, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle archive callback {Data} from {TelegramUserId}", data, telegramUserId);
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonSomethingWrong), ct);
        }
        finally
        {
            await _telegram.AnswerCallbackAsync(callbackQueryId, cancellationToken: ct);
        }
    }

    private async Task SetArchivedAsync(long chatId, long telegramUserId, string entity, Guid id, bool archived, CancellationToken ct)
    {
        Result result = entity switch
        {
            CallbackData.ArchiveEntityGrade => await _sender.Send(new SetOwnGradeArchivedCommand(telegramUserId, id, archived), ct),
            _ => await _sender.Send(new SetOwnAssignmentArchivedCommand(telegramUserId, id, archived), ct),
        };

        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        await _telegram.SendTextAsync(
            chatId,
            _text.Get(archived ? TextKeys.ArchiveDoneArchived : TextKeys.ArchiveDoneRestored),
            ct);

        // Re-render the list the user was working in so the moved item disappears.
        var kind = (entity, archived) switch
        {
            (CallbackData.ArchiveEntityGrade, true) => CallbackData.ArchiveGradesLive,
            (CallbackData.ArchiveEntityGrade, false) => CallbackData.ArchiveGradesArchived,
            (_, true) => CallbackData.ArchiveDeadlinesLive,
            (_, false) => CallbackData.ArchiveDeadlinesArchived,
        };
        await ShowListAsync(chatId, telegramUserId, kind, 1, ct);
    }

    private async Task ShowListAsync(long chatId, long telegramUserId, string kind, int page, CancellationToken ct)
    {
        var isGrade = kind is CallbackData.ArchiveGradesArchived or CallbackData.ArchiveGradesLive;
        var isArchivedList = kind is CallbackData.ArchiveDeadlinesArchived or CallbackData.ArchiveGradesArchived;

        var result = isGrade
            ? await _sender.Send(new GetOwnGradesForArchiveQuery(telegramUserId, isArchivedList, page, PageSize), ct)
            : await _sender.Send(new GetOwnDeadlinesForArchiveQuery(telegramUserId, isArchivedList, page, PageSize), ct);

        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        await _telegram.SendKeyboardAsync(chatId, RenderHeader(kind, result.Value), BuildRows(kind, isGrade, isArchivedList, result.Value), ct);
    }

    private string RenderHeader(string kind, ArchivePageDto page)
    {
        var titleKey = kind switch
        {
            CallbackData.ArchiveDeadlinesArchived => TextKeys.ArchiveListDeadlinesArchived,
            CallbackData.ArchiveGradesArchived => TextKeys.ArchiveListGradesArchived,
            CallbackData.ArchiveGradesLive => TextKeys.ArchiveListGradesLive,
            _ => TextKeys.ArchiveListDeadlinesLive,
        };

        var title = _text.Get(titleKey);
        if (page.Items.Count == 0)
        {
            var emptyKey = kind switch
            {
                CallbackData.ArchiveDeadlinesArchived => TextKeys.ArchiveEmptyDeadlinesArchived,
                CallbackData.ArchiveGradesArchived => TextKeys.ArchiveEmptyGradesArchived,
                CallbackData.ArchiveGradesLive => TextKeys.ArchiveEmptyGradesLive,
                _ => TextKeys.ArchiveEmptyDeadlinesLive,
            };
            return $"{title}\n\n{_text.Get(emptyKey)}";
        }

        var sb = new StringBuilder();
        sb.Append(title).Append("\n\n");
        sb.Append(_text.Get(TextKeys.CommonPage, page.Page, page.TotalPages));
        return sb.ToString();
    }

    private List<IReadOnlyList<InlineButton>> BuildRows(string kind, bool isGrade, bool isArchivedList, ArchivePageDto page)
    {
        var entity = isGrade ? CallbackData.ArchiveEntityGrade : CallbackData.ArchiveEntityDeadline;
        var icon = isArchivedList ? "♻️" : "\U0001f5c4️"; // ♻️ restore / 🗄️ archive

        var rows = page.Items
            .Select(item => (IReadOnlyList<InlineButton>)new[]
            {
                new InlineButton(
                    $"{icon} {item.Primary} · {item.Secondary}",
                    isArchivedList ? CallbackData.ArchiveRestore(entity, item.Id) : CallbackData.ArchiveDo(entity, item.Id)),
            })
            .ToList();

        var nav = new List<InlineButton>();
        if (page.HasPrevious)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonPrev), CallbackData.ArchiveList(kind, page.Page - 1)));
        }

        if (page.HasNext)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonNext), CallbackData.ArchiveList(kind, page.Page + 1)));
        }

        if (nav.Count > 0)
        {
            rows.Add(nav);
        }

        rows.Add(new[] { new InlineButton(_text.Get(TextKeys.ArchiveBtnBack), CallbackData.ArchiveMenu) });
        return rows;
    }

    private static int ParsePage(string raw) => int.TryParse(raw, out var p) && p > 0 ? p : 1;
}
