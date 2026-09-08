using System.Globalization;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Localization;
using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.CreateOwnAssignment;
using EduTrack.Application.Studies.Commands.CreateRecurringAssignment;
using EduTrack.Application.Studies.Commands.UpdateOwnAssignment;
using EduTrack.Application.Studies.Queries.ExportCalendar;
using EduTrack.Application.Studies.Queries.GetOwnAssignments;
using EduTrack.Application.Studies.Queries.GetSubjects;
using EduTrack.Bot.Web.Conversations;
using EduTrack.Bot.Web.Localization;
using EduTrack.Domain.Common;
using EduTrack.Domain.Studies;
using FluentValidation;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Handles the student deadline experience.
/// </summary>
public sealed class DeadlineModule
{
    public const string ScopeAll = "all";
    public const string ScopeToday = "today";
    public const string ScopeWeek = "week";
    public const string ScopeNext = "next";

    private const int ViewPageSize = 5;
    private const int EditPickPageSize = 8;
    private const int TypesPerRow = 3;

    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly IConversationStore _conversations;
    private readonly IDateTimeProvider _clock;
    private readonly IUiText _text;
    private readonly ILogger<DeadlineModule> _logger;

    public DeadlineModule(
        ISender sender,
        ITelegramSender telegram,
        IConversationStore conversations,
        IDateTimeProvider clock,
        IUiText text,
        ILogger<DeadlineModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _conversations = conversations;
        _clock = clock;
        _text = text;
        _logger = logger;
    }

    // --- Entry points from the update router --- //

    /// <summary>/export: send the caller's deadlines as an iCalendar (.ics) file.</summary>
    public async Task ExportAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var result = await _sender.Send(new ExportCalendarQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        var export = result.Value;
        if (export.EventCount == 0)
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.DeadlineNoExport), ct);
            return;
        }

        await _telegram.SendDocumentAsync(
            chatId,
            export.FileName,
            export.Content,
            _text.Get(TextKeys.DeadlineExportCaption),
            ct);
    }

    /// <summary>/deadlines, /today, /week, /next: list the caller's upcoming deadlines.</summary>
    public async Task ShowDeadlinesAsync(long chatId, long telegramUserId, string scope, int page, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var window = ResolveScope(scope, now);

        var result = await _sender.Send(
            new GetOwnAssignmentsQuery(telegramUserId, null, window.From, window.To, page, window.PageSize), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        var pageData = result.Value;
        var rows = new List<IReadOnlyList<InlineButton>>();
        if (window.Paged)
        {
            var nav = new List<InlineButton>();
            if (pageData.HasPrevious)
            {
                nav.Add(new InlineButton(_text.Get(TextKeys.CommonPrev), CallbackData.DeadlineView(scope, page - 1)));
            }

            if (pageData.HasNext)
            {
                nav.Add(new InlineButton(_text.Get(TextKeys.CommonNext), CallbackData.DeadlineView(scope, page + 1)));
            }

            if (nav.Count > 0)
            {
                rows.Add(nav);
            }
        }

        var title = _text.Get(window.TitleKey);
        await _telegram.SendKeyboardAsync(chatId, RenderDeadlinesPage(title, pageData, now, window.Paged), rows, ct);
    }

    public async Task StartAddAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var subjectRows = await SubjectRowsAsync(s => CallbackData.DeadlineWizardSubject(s.Id), ct);
        if (subjectRows.Count == 0)
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonNoSubjectsAdmin), ct);
            return;
        }

        var state = new ConversationState
        {
            Flow = ConversationFlow.DeadlineAdd,
            Step = DeadlineStep.Subject,
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineAddSelectSubject), WithCancel(subjectRows), ct);
    }

    public async Task StartEditAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var subjectRows = await SubjectRowsAsync(s => CallbackData.DeadlineWizardSubject(s.Id), ct);
        if (subjectRows.Count == 0)
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonNoSubjects), ct);
            return;
        }

        var state = new ConversationState
        {
            Flow = ConversationFlow.DeadlineEdit,
            Step = DeadlineStep.Subject,
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineEditSelectSubject), WithCancel(subjectRows), ct);
    }

    public async Task CancelAsync(long chatId, CancellationToken ct)
    {
        var state = await _conversations.GetAsync(chatId, ct);
        await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.CommonCancelled), ct);
    }

    /// <summary>Feeds a plain text message into the active deadline wizard.</summary>
    public async Task<bool> TryHandleTextAsync(long chatId, long telegramUserId, string text, CancellationToken ct)
    {
        var state = await _conversations.GetAsync(chatId, ct);
        if (state is null || !IsDeadlineFlow(state))
        {
            return false;
        }

        switch (state.Step)
        {
            case DeadlineStep.Title when !string.IsNullOrWhiteSpace(text):
                state.Title = text.Trim();
                await AdvanceToDescriptionAsync(chatId, state, ct);
                return true;

            case DeadlineStep.Title:
                await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineBadTitle), CancelRows(), ct);
                return true;

            case DeadlineStep.Description:
                state.Description = text.Trim();
                await AdvanceToDueAsync(chatId, state, ct);
                return true;

            case DeadlineStep.Due when TryParseDue(text, out var due):
                state.DueAtUtc = due;
                await AfterDueAsync(chatId, telegramUserId, state, ct);
                return true;

            case DeadlineStep.Due:
                await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineBadDue), CancelRows(), ct);
                return true;

            default:
                await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonUseButtons), ct);
                return true;
        }
    }

    public async Task HandleCallbackAsync(long chatId, long telegramUserId, string callbackQueryId, string data, CancellationToken ct)
    {
        try
        {
            var parts = CallbackData.Parts(data);
            var ns = parts.Length > 0 ? parts[0] : string.Empty;

            if (ns == CallbackData.DeadlineViewNamespace)
            {
                await HandleViewCallbackAsync(chatId, telegramUserId, parts, ct);
            }
            else if (ns == CallbackData.DeadlineWizardNamespace)
            {
                await HandleWizardCallbackAsync(chatId, telegramUserId, parts, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle deadline callback {Data} from {TelegramUserId}", data, telegramUserId);
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonSomethingWrong), ct);
        }
        finally
        {
            await _telegram.AnswerCallbackAsync(callbackQueryId, cancellationToken: ct);
        }
    }

    // --- Viewing --- //

    private async Task HandleViewCallbackAsync(long chatId, long telegramUserId, string[] parts, CancellationToken ct)
    {
        // dv:{scope}:{page}
        if (parts.Length < 3)
        {
            return;
        }

        var scope = parts[1];
        var page = int.TryParse(parts[2], out var p) ? p : 1;
        await ShowDeadlinesAsync(chatId, telegramUserId, scope, page, ct);
    }

    // --- Wizard callbacks --- //

    private async Task HandleWizardCallbackAsync(long chatId, long telegramUserId, string[] parts, CancellationToken ct)
    {
        var action = parts.Length > 1 ? parts[1] : string.Empty;

        if (action == "x")
        {
            await CancelAsync(chatId, ct);
            return;
        }

        var state = await _conversations.GetAsync(chatId, ct);
        if (state is null || !IsDeadlineFlow(state))
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.DeadlineWizardExpired), ct);
            return;
        }

        switch (action)
        {
            case "sub" when state.Step == DeadlineStep.Subject && parts.Length >= 3 && Guid.TryParse(parts[2], out var subjectId):
                await OnSubjectChosenAsync(chatId, telegramUserId, state, subjectId, ct);
                break;

            case "item" when state.Step == DeadlineStep.Item && parts.Length >= 3 && Guid.TryParse(parts[2], out var assignmentId):
                state.AssignmentId = assignmentId;
                await AdvanceToTypeAsync(chatId, state, ct);
                break;

            case "type" when state.Step == DeadlineStep.Type && parts.Length >= 3 && TryParseType(parts[2], out var type):
                state.AssignmentType = (int)type;
                await AdvanceToTitleAsync(chatId, state, ct);
                break;

            case "skip" when state.Step == DeadlineStep.Description:
                state.Description = null;
                await AdvanceToDueAsync(chatId, state, ct);
                break;

            case "rep" when state.Step == DeadlineStep.Repeat && parts.Length >= 3 && parts[2] == "none":
                state.RecurrenceFrequency = null;
                state.RecurrenceCount = null;
                await AdvanceToConfirmAsync(chatId, telegramUserId, state, ct);
                break;

            case "rep" when state.Step == DeadlineStep.Repeat && parts.Length >= 3
                && int.TryParse(parts[2], out var freq) && Enum.IsDefined(typeof(RecurrenceFrequency), freq):
                state.RecurrenceFrequency = freq;
                await AdvanceToCountAsync(chatId, state, ct);
                break;

            case "cnt" when state.Step == DeadlineStep.Count && parts.Length >= 3 && int.TryParse(parts[2], out var count):
                state.RecurrenceCount = count;
                await AdvanceToConfirmAsync(chatId, telegramUserId, state, ct);
                break;

            case "ok" when state.Step == DeadlineStep.Confirm:
                await ExecuteAsync(chatId, telegramUserId, state, ct);
                break;

            default:
                await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonUseButtons), ct);
                break;
        }
    }

    private async Task OnSubjectChosenAsync(long chatId, long telegramUserId, ConversationState state, Guid subjectId, CancellationToken ct)
    {
        var subjectsResult = await _sender.Send(new GetSubjectsQuery(), ct);
        var subject = subjectsResult.IsSuccess ? subjectsResult.Value.FirstOrDefault(s => s.Id == subjectId) : null;

        if (subject is null)
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonSubjectGone), ct);
            return;
        }

        state.SubjectId = subject.Id;
        state.SubjectName = subject.Name;

        if (state.Flow == ConversationFlow.DeadlineEdit)
        {
            var listResult = await _sender.Send(
                new GetOwnAssignmentsQuery(telegramUserId, subject.Id, null, null, 1, EditPickPageSize), ct);
            if (listResult.IsFailure)
            {
                await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Error(listResult.Error), ct);
                return;
            }

            if (listResult.Value.Items.Count == 0)
            {
                await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineNoneInSubject, subject.Name), ct);
                return;
            }

            state.Step = DeadlineStep.Item;

            var rows = listResult.Value.Items
                .Select(a => (IReadOnlyList<InlineButton>)new[]
                {
                    new InlineButton($"{a.Title} · {a.DueAtUtc:yyyy-MM-dd HH:mm}", CallbackData.DeadlineWizardItem(a.Id)),
                })
                .ToList();

            await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineSelectToEdit), WithCancel(rows), ct);
            return;
        }

        await AdvanceToTypeAsync(chatId, state, ct);
    }

    // --- Step transitions --- //

    private async Task AdvanceToTypeAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Type;

        var buttons = Enum.GetValues<AssignmentType>()
            .Select(t => new InlineButton(TypeLabel(t), CallbackData.DeadlineWizardType((int)t)))
            .ToList();

        var rows = new List<IReadOnlyList<InlineButton>>();
        for (var i = 0; i < buttons.Count; i += TypesPerRow)
        {
            rows.Add(buttons.Skip(i).Take(TypesPerRow).ToArray());
        }

        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineChooseType), WithCancel(rows), ct);
    }

    private async Task AdvanceToTitleAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Title;
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineSendTitle), CancelRows(), ct);
    }

    private async Task AdvanceToDescriptionAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Description;
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineSendDescription), SkipCancelRows(), ct);
    }

    private async Task AdvanceToDueAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Due;
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineSendDue), CancelRows(), ct);
    }

    /// <summary>After the due date: the add wizard offers recurrence; edit goes straight to confirm.</summary>
    private async Task AfterDueAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
        if (state.Flow == ConversationFlow.DeadlineEdit)
        {
            await AdvanceToConfirmAsync(chatId, telegramUserId, state, ct);
            return;
        }

        await AdvanceToRepeatAsync(chatId, state, ct);
    }

    private async Task AdvanceToRepeatAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Repeat;

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[] { new InlineButton(_text.Get(TextKeys.DeadlineRepeatOnce), CallbackData.DeadlineWizardRepeatOnce) },
            new[]
            {
                new InlineButton(_text.Get(TextKeys.RecurrenceDaily), CallbackData.DeadlineWizardRepeat((int)RecurrenceFrequency.Daily)),
                new InlineButton(_text.Get(TextKeys.RecurrenceWeekly), CallbackData.DeadlineWizardRepeat((int)RecurrenceFrequency.Weekly)),
                new InlineButton(_text.Get(TextKeys.RecurrenceMonthly), CallbackData.DeadlineWizardRepeat((int)RecurrenceFrequency.Monthly)),
            },
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineRepeatPrompt), WithCancel(rows), ct);
    }

    private async Task AdvanceToCountAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Count;

        var buttons = CountPresets
            .Select(n => new InlineButton($"×{n}", CallbackData.DeadlineWizardCount(n)))
            .ToArray();

        var rows = new List<IReadOnlyList<InlineButton>> { buttons };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineCountPrompt), WithCancel(rows), ct);
    }

    private async Task AdvanceToConfirmAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Confirm;

        var type = (AssignmentType)(state.AssignmentType ?? 0);
        var summary =
            _text.Get(TextKeys.CommonConfirmHeader) + "\n" +
            $"{_text.Get(TextKeys.LabelSubject)}: {state.SubjectName}\n" +
            $"{_text.Get(TextKeys.LabelType)}: {TypeLabel(type)}\n" +
            $"{_text.Get(TextKeys.LabelTitle)}: {state.Title}\n" +
            $"{_text.Get(TextKeys.LabelDescription)}: {state.Description ?? _text.Get(TextKeys.CommonNone)}\n" +
            $"{_text.Get(TextKeys.LabelDue)}: {(state.DueAtUtc ?? _clock.UtcNow):yyyy-MM-dd HH:mm} UTC";

        if (state.RecurrenceFrequency is int f && state.RecurrenceCount is int c)
        {
            summary += "\n" + _text.Get(TextKeys.DeadlineRepeatSummary, FrequencyLabel((RecurrenceFrequency)f), c);
        }

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton(_text.Get(TextKeys.CommonConfirm), CallbackData.DeadlineWizardConfirm),
                new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.DeadlineWizardCancel),
            },
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, summary, rows, ct);
    }

    private async Task ExecuteAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
        if (state.Flow == ConversationFlow.DeadlineAdd
            && state.RecurrenceFrequency is int freq && state.RecurrenceCount is int count)
        {
            await ExecuteRecurringAsync(chatId, telegramUserId, state, (RecurrenceFrequency)freq, count, ct);
            return;
        }

        var type = (AssignmentType)(state.AssignmentType ?? 0);
        var title = state.Title ?? string.Empty;
        var dueAtUtc = state.DueAtUtc ?? _clock.UtcNow;

        Result<AssignmentDto> result;
        try
        {
            result = state.Flow == ConversationFlow.DeadlineEdit
                ? await _sender.Send(new UpdateOwnAssignmentCommand(telegramUserId, state.AssignmentId ?? Guid.Empty, type, title, state.Description, dueAtUtc), ct)
                : await _sender.Send(new CreateOwnAssignmentCommand(telegramUserId, state.SubjectId ?? Guid.Empty, type, title, state.Description, dueAtUtc), ct);
        }
        catch (ValidationException ex)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineCouldNotSave, _text.ValidationDetails(ex)), ct);
            return;
        }

        if (result.IsFailure)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Error(result.Error), ct);
            return;
        }

        var header = state.Flow == ConversationFlow.DeadlineEdit ? _text.Get(TextKeys.DeadlineUpdated) : _text.Get(TextKeys.DeadlineAdded);
        await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, $"{header}\n\n{RenderDeadline(result.Value)}", ct);
    }

    private async Task ExecuteRecurringAsync(long chatId, long telegramUserId, ConversationState state, RecurrenceFrequency frequency, int count, CancellationToken ct)
    {
        var type = (AssignmentType)(state.AssignmentType ?? 0);
        var title = state.Title ?? string.Empty;
        var firstDueAtUtc = state.DueAtUtc ?? _clock.UtcNow;

        Result<int> result;
        try
        {
            result = await _sender.Send(new CreateRecurringAssignmentCommand(
                telegramUserId, state.SubjectId ?? Guid.Empty, type, title, state.Description, firstDueAtUtc, frequency, 1, count), ct);
        }
        catch (ValidationException ex)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineCouldNotSave, _text.ValidationDetails(ex)), ct);
            return;
        }

        if (result.IsFailure)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Error(result.Error), ct);
            return;
        }

        await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.DeadlineRecurringAdded, result.Value), ct);
    }

    // --- Rendering & helpers --- //

    private static readonly int[] CountPresets = { 4, 8, 12 };

    private string FrequencyLabel(RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily => _text.Get(TextKeys.RecurrenceDaily),
        RecurrenceFrequency.Weekly => _text.Get(TextKeys.RecurrenceWeekly),
        RecurrenceFrequency.Monthly => _text.Get(TextKeys.RecurrenceMonthly),
        _ => _text.Get(TextKeys.RecurrenceWeekly),
    };

    private static bool IsDeadlineFlow(ConversationState state) =>
        state.Flow == ConversationFlow.DeadlineAdd || state.Flow == ConversationFlow.DeadlineEdit;

    private string TypeLabel(AssignmentType type) => _text.Get(TextKeys.Type(type.ToString()));

    private async Task<List<IReadOnlyList<InlineButton>>> SubjectRowsAsync(Func<SubjectDto, string> callback, CancellationToken ct)
    {
        var result = await _sender.Send(new GetSubjectsQuery(), ct);
        if (result.IsFailure)
        {
            return new List<IReadOnlyList<InlineButton>>();
        }

        return result.Value
            .Select(s => (IReadOnlyList<InlineButton>)new[] { new InlineButton(s.Name, callback(s)) })
            .ToList();
    }

    private List<IReadOnlyList<InlineButton>> WithCancel(List<IReadOnlyList<InlineButton>> rows)
    {
        var copy = new List<IReadOnlyList<InlineButton>>(rows)
        {
            new[] { new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.DeadlineWizardCancel) },
        };
        return copy;
    }

    private List<IReadOnlyList<InlineButton>> SkipCancelRows() => new()
    {
        new[]
        {
            new InlineButton(_text.Get(TextKeys.CommonSkip), CallbackData.DeadlineWizardSkip),
            new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.DeadlineWizardCancel),
        },
    };

    private List<IReadOnlyList<InlineButton>> CancelRows() => new()
    {
        new[] { new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.DeadlineWizardCancel) },
    };

    private static (DateTime? From, DateTime? To, int PageSize, bool Paged, string TitleKey) ResolveScope(string scope, DateTime nowUtc) => scope switch
    {
        ScopeToday => (nowUtc, nowUtc.Date.AddDays(1), ViewPageSize, true, TextKeys.DeadlineScopeToday),
        ScopeWeek => (nowUtc, nowUtc.AddDays(7), ViewPageSize, true, TextKeys.DeadlineScopeWeek),
        ScopeNext => (nowUtc, (DateTime?)null, 1, false, TextKeys.DeadlineScopeNext),
        _ => (nowUtc, (DateTime?)null, ViewPageSize, true, TextKeys.DeadlineScopeUpcoming),
    };

    private string RenderDeadlinesPage(string title, AssignmentsPageDto page, DateTime nowUtc, bool paged)
    {
        if (page.Items.Count == 0)
        {
            return $"{title}\n\n{_text.Get(TextKeys.DeadlineNoneUpcoming)}";
        }

        var lines = page.Items.Select((a, i) =>
        {
            var number = paged ? (page.Page - 1) * page.PageSize + i + 1 : i + 1;
            return
                $"{number}. {a.Title} ({TypeLabel(a.Type)}) — {a.SubjectName}\n" +
                $"   {_text.Get(TextKeys.LabelDue)}: {a.DueAtUtc:yyyy-MM-dd HH:mm} UTC ({FormatRemaining(a.DueAtUtc, nowUtc)})";
        });

        var body = string.Join("\n", lines);
        var footer = paged ? "\n\n" + _text.Get(TextKeys.CommonPage, page.Page, page.TotalPages) : string.Empty;
        return $"{title}\n\n{body}{footer}";
    }

    private string RenderDeadline(AssignmentDto a) =>
        $"{_text.Get(TextKeys.LabelSubject)}: {a.SubjectName}\n" +
        $"{_text.Get(TextKeys.LabelType)}: {TypeLabel(a.Type)}\n" +
        $"{_text.Get(TextKeys.LabelTitle)}: {a.Title}\n" +
        $"{_text.Get(TextKeys.LabelDescription)}: {a.Description ?? _text.Get(TextKeys.CommonNone)}\n" +
        $"{_text.Get(TextKeys.LabelDue)}: {a.DueAtUtc:yyyy-MM-dd HH:mm} UTC";

    private string FormatRemaining(DateTime dueUtc, DateTime nowUtc)
    {
        var span = dueUtc - nowUtc;
        if (span < TimeSpan.Zero)
        {
            return _text.Get(TextKeys.RemainingOverdue);
        }

        if (span.TotalDays >= 1)
        {
            return _text.Get(TextKeys.RemainingDays, (int)span.TotalDays);
        }

        if (span.TotalHours >= 1)
        {
            return _text.Get(TextKeys.RemainingHours, (int)span.TotalHours);
        }

        return _text.Get(TextKeys.RemainingMinutes, (int)span.TotalMinutes);
    }

    private static bool TryParseType(string text, out AssignmentType type)
    {
        if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw)
            && Enum.IsDefined(typeof(AssignmentType), raw))
        {
            type = (AssignmentType)raw;
            return true;
        }

        type = default;
        return false;
    }

    private static bool TryParseDue(string text, out DateTime dueUtc)
    {
        var trimmed = text.Trim();
        if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            dueUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        dueUtc = default;
        return false;
    }
}
