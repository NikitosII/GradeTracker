using System.Globalization;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Localization;
using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.AddOwnGrade;
using EduTrack.Application.Studies.Commands.UpdateOwnGrade;
using EduTrack.Application.Studies.Queries.GetOwnGrades;
using EduTrack.Application.Studies.Queries.GetSubjects;
using EduTrack.Bot.Web.Conversations;
using EduTrack.Bot.Web.Localization;
using EduTrack.Domain.Common;
using FluentValidation;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Handles the student grade experience.
/// </summary>
public sealed class GradeModule
{
    private const int ViewPageSize = 5;
    private const int EditPickPageSize = 8;

    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly IConversationStore _conversations;
    private readonly IDateTimeProvider _clock;
    private readonly IUiText _text;
    private readonly ILogger<GradeModule> _logger;

    public GradeModule(
        ISender sender,
        ITelegramSender telegram,
        IConversationStore conversations,
        IDateTimeProvider clock,
        IUiText text,
        ILogger<GradeModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _conversations = conversations;
        _clock = clock;
        _text = text;
        _logger = logger;
    }

    // --- Entry points from the update router --- //

    /// <summary>/grades and /subjects: pick a subject (or all) to view grades.</summary>
    public async Task ShowGradesMenuAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[] { new InlineButton(_text.Get(TextKeys.GradeAllTitle), CallbackData.ViewSubject(Guid.Empty, 1)) },
        };
        rows.AddRange(await SubjectRowsAsync(s => CallbackData.ViewSubject(s.Id, 1), ct));

        rows.Add(new[]
        {
            new InlineButton(_text.Get(TextKeys.GradeBtnAdd), CallbackData.GradeAdd),
            new InlineButton(_text.Get(TextKeys.GradeBtnEdit), CallbackData.GradeEdit),
        });

        await _telegram.SendKeyboardAsync(chatId, _text.Get(TextKeys.GradeChooseSubjectView), rows, ct);
    }

    public async Task StartAddAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var subjectRows = await SubjectRowsAsync(s => CallbackData.WizardSubject(s.Id), ct);
        if (subjectRows.Count == 0)
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonNoSubjectsAdmin), ct);
            return;
        }

        var state = new ConversationState
        {
            Flow = ConversationFlow.GradeAdd,
            Step = GradeStep.Subject,
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeAddSelectSubject), WithCancel(subjectRows), ct);
    }

    public async Task StartEditAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var subjectRows = await SubjectRowsAsync(s => CallbackData.WizardSubject(s.Id), ct);
        if (subjectRows.Count == 0)
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonNoSubjects), ct);
            return;
        }

        var state = new ConversationState
        {
            Flow = ConversationFlow.GradeEdit,
            Step = GradeStep.Subject,
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeEditSelectSubject), WithCancel(subjectRows), ct);
    }

    public async Task CancelAsync(long chatId, CancellationToken ct)
    {
        var state = await _conversations.GetAsync(chatId, ct);
        await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.CommonCancelled), ct);
    }

    /// <summary>Feeds a plain text message into the active wizard.</summary>
    public async Task<bool> TryHandleTextAsync(long chatId, long telegramUserId, string text, CancellationToken ct)
    {
        var state = await _conversations.GetAsync(chatId, ct);
        if (state is null || (state.Flow != ConversationFlow.GradeAdd && state.Flow != ConversationFlow.GradeEdit))
        {
            return false;
        }

        switch (state.Step)
        {
            case GradeStep.Value when TryParseValue(text, out var value):
                await OnValueChosenAsync(chatId, telegramUserId, state, value, ct);
                return true;

            case GradeStep.Comment:
                state.Comment = text.Trim();
                await AdvanceToDateAsync(chatId, state, ct);
                return true;

            case GradeStep.Date when TryParseDate(text, out var date):
                state.OccurredAt = date;
                await AdvanceToWeightAsync(chatId, state, ct);
                return true;

            case GradeStep.Date:
                await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeBadDate), SkipCancelRows(), ct);
                return true;

            case GradeStep.Weight when TryParseWeight(text, out var weight):
                state.Weight = weight;
                await AdvanceToConfirmAsync(chatId, telegramUserId, state, ct);
                return true;

            case GradeStep.Weight:
                await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeBadWeight), SkipCancelRows(), ct);
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

            if (ns == CallbackData.ViewNamespace)
            {
                await HandleViewCallbackAsync(chatId, telegramUserId, parts, ct);
            }
            else if (ns == CallbackData.WizardNamespace)
            {
                await HandleWizardCallbackAsync(chatId, telegramUserId, parts, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle callback {Data} from {TelegramUserId}", data, telegramUserId);
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
        // gv:sub:{subjectId}:{page}  |  gv:add  |  gv:edit
        var action = parts.Length > 1 ? parts[1] : string.Empty;

        if (action == "add")
        {
            await StartAddAsync(chatId, telegramUserId, ct);
            return;
        }

        if (action == "edit")
        {
            await StartEditAsync(chatId, telegramUserId, ct);
            return;
        }

        if (parts.Length < 4 || parts[1] != "sub")
        {
            return;
        }

        var subjectId = Guid.TryParse(parts[2], out var sid) && sid != Guid.Empty ? sid : (Guid?)null;
        var page = int.TryParse(parts[3], out var p) ? p : 1;

        var result = await _sender.Send(new GetOwnGradesQuery(telegramUserId, subjectId, page, ViewPageSize), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        var pageData = result.Value;
        var title = pageData.Items.FirstOrDefault()?.SubjectName
            ?? (subjectId is null ? _text.Get(TextKeys.GradeAllTitle) : _text.Get(TextKeys.GradeTitleFallback));

        var rows = new List<IReadOnlyList<InlineButton>>();
        var nav = new List<InlineButton>();
        if (pageData.HasPrevious)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonPrev), CallbackData.ViewSubject(subjectId ?? Guid.Empty, page - 1)));
        }

        if (pageData.HasNext)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonNext), CallbackData.ViewSubject(subjectId ?? Guid.Empty, page + 1)));
        }

        if (nav.Count > 0)
        {
            rows.Add(nav);
        }

        await _telegram.SendKeyboardAsync(chatId, RenderGradesPage(title, pageData), rows, ct);
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
        if (state is null)
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.GradeWizardExpired), ct);
            return;
        }

        switch (action)
        {
            case "sub" when state.Step == GradeStep.Subject && parts.Length >= 3 && Guid.TryParse(parts[2], out var subjectId):
                await OnSubjectChosenAsync(chatId, telegramUserId, state, subjectId, ct);
                break;

            case "grade" when state.Step == GradeStep.Grade && parts.Length >= 3 && Guid.TryParse(parts[2], out var gradeId):
                state.GradeId = gradeId;
                await AdvanceToValueAsync(chatId, state, ct);
                break;

            case "val" when state.Step == GradeStep.Value && parts.Length >= 3 && TryParseValue(parts[2], out var value):
                await OnValueChosenAsync(chatId, telegramUserId, state, value, ct);
                break;

            case "skip":
                await OnSkipAsync(chatId, telegramUserId, state, ct);
                break;

            case "ok" when state.Step == GradeStep.Confirm:
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

        if (state.Flow == ConversationFlow.GradeEdit)
        {
            var gradesResult = await _sender.Send(new GetOwnGradesQuery(telegramUserId, subject.Id, 1, EditPickPageSize), ct);
            if (gradesResult.IsFailure)
            {
                await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Error(gradesResult.Error), ct);
                return;
            }

            if (gradesResult.Value.Items.Count == 0)
            {
                await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeNoneInSubject, subject.Name), ct);
                return;
            }

            state.Step = GradeStep.Grade;

            var rows = gradesResult.Value.Items
                .Select(g => (IReadOnlyList<InlineButton>)new[]
                {
                    new InlineButton($"{g.Value} · {g.OccurredAt:yyyy-MM-dd}", CallbackData.WizardGrade(g.Id)),
                })
                .ToList();

            await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeSelectToEdit), WithCancel(rows), ct);
            return;
        }

        await AdvanceToValueAsync(chatId, state, ct);
    }

    private async Task OnValueChosenAsync(long chatId, long telegramUserId, ConversationState state, int value, CancellationToken ct)
    {
        state.Value = value;
        state.Step = GradeStep.Comment;
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeSendComment), SkipCancelRows(), ct);
    }

    private async Task OnSkipAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
        switch (state.Step)
        {
            case GradeStep.Comment:
                state.Comment = null;
                await AdvanceToDateAsync(chatId, state, ct);
                break;
            case GradeStep.Date:
                state.OccurredAt = _clock.UtcNow;
                await AdvanceToWeightAsync(chatId, state, ct);
                break;
            case GradeStep.Weight:
                state.Weight = 1m;
                await AdvanceToConfirmAsync(chatId, telegramUserId, state, ct);
                break;
            default:
                await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.GradeNothingToSkip), ct);
                break;
        }
    }

    // --- Step transitions --- //

    private async Task AdvanceToValueAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = GradeStep.Value;

        var valueRow = Enumerable.Range(Domain.Studies.Grade.MinValue, Domain.Studies.Grade.MaxValue - Domain.Studies.Grade.MinValue + 1)
            .Select(v => new InlineButton(v.ToString(CultureInfo.InvariantCulture), CallbackData.WizardValue(v)))
            .ToArray();

        var rows = new List<IReadOnlyList<InlineButton>> { valueRow };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeChooseValue), WithCancel(rows), ct);
    }

    private async Task AdvanceToDateAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = GradeStep.Date;
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeSendDate), SkipCancelRows(), ct);
    }

    private async Task AdvanceToWeightAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = GradeStep.Weight;
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeSendWeight), SkipCancelRows(), ct);
    }

    private async Task AdvanceToConfirmAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
        state.Step = GradeStep.Confirm;

        var summary =
            _text.Get(TextKeys.CommonConfirmHeader) + "\n" +
            $"{_text.Get(TextKeys.LabelSubject)}: {state.SubjectName}\n" +
            $"{_text.Get(TextKeys.LabelGrade)}: {state.Value}\n" +
            $"{_text.Get(TextKeys.LabelWeight)}: {(state.Weight ?? 1m).ToString(CultureInfo.InvariantCulture)}\n" +
            $"{_text.Get(TextKeys.LabelComment)}: {state.Comment ?? _text.Get(TextKeys.CommonNone)}\n" +
            $"{_text.Get(TextKeys.LabelDate)}: {(state.OccurredAt ?? _clock.UtcNow):yyyy-MM-dd}";

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton(_text.Get(TextKeys.CommonConfirm), CallbackData.WizardConfirm),
                new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.WizardCancel),
            },
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, summary, rows, ct);
    }

    private async Task ExecuteAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
        var value = state.Value ?? Domain.Studies.Grade.MinValue;
        var weight = state.Weight ?? 1m;
        var occurredAt = state.OccurredAt ?? _clock.UtcNow;

        Result<GradeDto> result;
        try
        {
            result = state.Flow == ConversationFlow.GradeEdit
                ? await _sender.Send(new UpdateOwnGradeCommand(telegramUserId, state.GradeId ?? Guid.Empty, value, weight, state.Comment, occurredAt), ct)
                : await _sender.Send(new AddOwnGradeCommand(telegramUserId, state.SubjectId ?? Guid.Empty, value, weight, state.Comment, occurredAt), ct);
        }
        catch (ValidationException ex)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.GradeCouldNotSave, _text.ValidationDetails(ex)), ct);
            return;
        }

        if (result.IsFailure)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Error(result.Error), ct);
            return;
        }

        var header = state.Flow == ConversationFlow.GradeEdit ? _text.Get(TextKeys.GradeUpdated) : _text.Get(TextKeys.GradeAdded);
        await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, $"{header}\n\n{RenderGrade(result.Value)}", ct);
    }

    // --- Rendering & helpers --- //

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
            new[] { new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.WizardCancel) },
        };
        return copy;
    }

    private List<IReadOnlyList<InlineButton>> SkipCancelRows() => new()
    {
        new[]
        {
            new InlineButton(_text.Get(TextKeys.CommonSkip), CallbackData.WizardSkip),
            new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.WizardCancel),
        },
    };

    private string RenderGradesPage(string title, GradesPageDto page)
    {
        if (page.Items.Count == 0)
        {
            return $"{title}\n\n{_text.Get(TextKeys.GradeNoneYet)}";
        }

        var lines = page.Items.Select((g, i) =>
        {
            var comment = string.IsNullOrWhiteSpace(g.Comment) ? string.Empty : $" — {g.Comment}";
            return $"{(page.Page - 1) * page.PageSize + i + 1}. {g.Value} · {g.OccurredAt:yyyy-MM-dd}{comment}";
        });

        var body = string.Join("\n", lines);
        var average = page.Average is { } avg
            ? "\n\n" + _text.Get(TextKeys.GradeAverage, avg.ToString(CultureInfo.InvariantCulture))
            : string.Empty;
        return $"{title}\n\n{body}{average}\n\n{_text.Get(TextKeys.CommonPage, page.Page, page.TotalPages)}";
    }

    private string RenderGrade(GradeDto g) =>
        $"{_text.Get(TextKeys.LabelSubject)}: {g.SubjectName}\n" +
        $"{_text.Get(TextKeys.LabelGrade)}: {g.Value}\n" +
        $"{_text.Get(TextKeys.LabelWeight)}: {g.Weight.ToString(CultureInfo.InvariantCulture)}\n" +
        $"{_text.Get(TextKeys.LabelComment)}: {g.Comment ?? _text.Get(TextKeys.CommonNone)}\n" +
        $"{_text.Get(TextKeys.LabelDate)}: {g.OccurredAt:yyyy-MM-dd}";

    private static bool TryParseValue(string text, out int value)
    {
        value = 0;
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= Domain.Studies.Grade.MinValue
            && value <= Domain.Studies.Grade.MaxValue;
    }

    private static bool TryParseDate(string text, out DateTime dateUtc)
    {
        if (DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            dateUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        dateUtc = default;
        return false;
    }

    private static bool TryParseWeight(string text, out decimal weight)
        => decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out weight) && weight > 0m;
}
