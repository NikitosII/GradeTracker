using System.Globalization;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.CreateOwnAssignment;
using EduTrack.Application.Studies.Commands.UpdateOwnAssignment;
using EduTrack.Application.Studies.Queries.GetOwnAssignments;
using EduTrack.Application.Studies.Queries.GetSubjects;
using EduTrack.Bot.Web.Conversations;
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
    private readonly ILogger<DeadlineModule> _logger;

    public DeadlineModule(
        ISender sender,
        ITelegramSender telegram,
        IConversationStore conversations,
        IDateTimeProvider clock,
        ILogger<DeadlineModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _conversations = conversations;
        _clock = clock;
        _logger = logger;
    }

    // --- Entry points from the update router --- //

    /// <summary>/deadlines, /today, /week, /next: list the caller's upcoming deadlines.</summary>
    public async Task ShowDeadlinesAsync(long chatId, long telegramUserId, string scope, int page, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var window = ResolveScope(scope, now);

        var result = await _sender.Send(
            new GetOwnAssignmentsQuery(telegramUserId, null, window.From, window.To, page, window.PageSize), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return;
        }

        var pageData = result.Value;
        var rows = new List<IReadOnlyList<InlineButton>>();
        if (window.Paged)
        {
            var nav = new List<InlineButton>();
            if (pageData.HasPrevious)
            {
                nav.Add(new InlineButton("◀ Prev", CallbackData.DeadlineView(scope, page - 1)));
            }

            if (pageData.HasNext)
            {
                nav.Add(new InlineButton("Next ▶", CallbackData.DeadlineView(scope, page + 1)));
            }

            if (nav.Count > 0)
            {
                rows.Add(nav);
            }
        }

        await _telegram.SendKeyboardAsync(chatId, RenderDeadlinesPage(window.Title, pageData, now, window.Paged), rows, ct);
    }

    public async Task StartAddAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var subjectRows = await SubjectRowsAsync(s => CallbackData.DeadlineWizardSubject(s.Id), ct);
        if (subjectRows.Count == 0)
        {
            await _telegram.SendTextAsync(chatId, "No subjects are available yet. Ask an administrator to add some.", ct);
            return;
        }

        await _conversations.SetAsync(chatId, new ConversationState
        {
            Flow = ConversationFlow.DeadlineAdd,
            Step = DeadlineStep.Subject,
        }, ct);

        await _telegram.SendKeyboardAsync(chatId, "Add a deadline.\nSelect a subject:", WithCancel(subjectRows), ct);
    }

    public async Task StartEditAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var subjectRows = await SubjectRowsAsync(s => CallbackData.DeadlineWizardSubject(s.Id), ct);
        if (subjectRows.Count == 0)
        {
            await _telegram.SendTextAsync(chatId, "No subjects are available yet.", ct);
            return;
        }

        await _conversations.SetAsync(chatId, new ConversationState
        {
            Flow = ConversationFlow.DeadlineEdit,
            Step = DeadlineStep.Subject,
        }, ct);

        await _telegram.SendKeyboardAsync(chatId, "Edit a deadline.\nSelect a subject:", WithCancel(subjectRows), ct);
    }

    public async Task CancelAsync(long chatId, CancellationToken ct)
    {
        await _conversations.RemoveAsync(chatId, ct);
        await _telegram.SendTextAsync(chatId, "Cancelled.", ct);
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
                await _telegram.SendKeyboardAsync(chatId, "Please send a non-empty title, or tap Cancel.", CancelRows(), ct);
                return true;

            case DeadlineStep.Description:
                state.Description = text.Trim();
                await AdvanceToDueAsync(chatId, state, ct);
                return true;

            case DeadlineStep.Due when TryParseDue(text, out var due):
                state.DueAtUtc = due;
                await AdvanceToConfirmAsync(chatId, telegramUserId, state, ct);
                return true;

            case DeadlineStep.Due:
                await _telegram.SendKeyboardAsync(chatId, "Please send the due date as YYYY-MM-DD HH:mm (or YYYY-MM-DD), or tap Cancel.", CancelRows(), ct);
                return true;

            default:
                await _telegram.SendTextAsync(chatId, "Please use the buttons above.", ct);
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
            await _telegram.SendTextAsync(chatId, "Something went wrong. Please try again.", ct);
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
            await _telegram.SendTextAsync(chatId, "This wizard has expired. Start again with /deadline_add or /deadline_edit.", ct);
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

            case "ok" when state.Step == DeadlineStep.Confirm:
                await ExecuteAsync(chatId, telegramUserId, state, ct);
                break;

            default:
                await _telegram.SendTextAsync(chatId, "Please use the buttons above.", ct);
                break;
        }
    }

    private async Task OnSubjectChosenAsync(long chatId, long telegramUserId, ConversationState state, Guid subjectId, CancellationToken ct)
    {
        var subjectsResult = await _sender.Send(new GetSubjectsQuery(), ct);
        var subject = subjectsResult.IsSuccess ? subjectsResult.Value.FirstOrDefault(s => s.Id == subjectId) : null;

        if (subject is null)
        {
            await _telegram.SendTextAsync(chatId, "That subject is no longer available.", ct);
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
                await _conversations.RemoveAsync(chatId, ct);
                await _telegram.SendTextAsync(chatId, listResult.Error.Message, ct);
                return;
            }

            if (listResult.Value.Items.Count == 0)
            {
                await _conversations.RemoveAsync(chatId, ct);
                await _telegram.SendTextAsync(chatId, $"You have no deadlines in {subject.Name} yet.", ct);
                return;
            }

            state.Step = DeadlineStep.Item;
            await _conversations.SetAsync(chatId, state, ct);

            var rows = listResult.Value.Items
                .Select(a => (IReadOnlyList<InlineButton>)new[]
                {
                    new InlineButton($"{a.Title} · {a.DueAtUtc:yyyy-MM-dd HH:mm}", CallbackData.DeadlineWizardItem(a.Id)),
                })
                .ToList();

            await _telegram.SendKeyboardAsync(chatId, "Select the deadline to edit:", WithCancel(rows), ct);
            return;
        }

        await AdvanceToTypeAsync(chatId, state, ct);
    }

    // --- Step transitions --- //

    private async Task AdvanceToTypeAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Type;
        await _conversations.SetAsync(chatId, state, ct);

        var buttons = Enum.GetValues<AssignmentType>()
            .Select(t => new InlineButton(t.ToString(), CallbackData.DeadlineWizardType((int)t)))
            .ToList();

        var rows = new List<IReadOnlyList<InlineButton>>();
        for (var i = 0; i < buttons.Count; i += TypesPerRow)
        {
            rows.Add(buttons.Skip(i).Take(TypesPerRow).ToArray());
        }

        await _telegram.SendKeyboardAsync(chatId, "Choose the event type:", WithCancel(rows), ct);
    }

    private async Task AdvanceToTitleAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Title;
        await _conversations.SetAsync(chatId, state, ct);
        await _telegram.SendKeyboardAsync(chatId, "Send a title for this deadline:", CancelRows(), ct);
    }

    private async Task AdvanceToDescriptionAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Description;
        await _conversations.SetAsync(chatId, state, ct);
        await _telegram.SendKeyboardAsync(chatId, "Send a description, or tap Skip.", SkipCancelRows(), ct);
    }

    private async Task AdvanceToDueAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Due;
        await _conversations.SetAsync(chatId, state, ct);
        await _telegram.SendKeyboardAsync(chatId, "Send the due date as YYYY-MM-DD HH:mm (UTC), or just YYYY-MM-DD.", CancelRows(), ct);
    }

    private async Task AdvanceToConfirmAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
        state.Step = DeadlineStep.Confirm;
        await _conversations.SetAsync(chatId, state, ct);

        var type = (AssignmentType)(state.AssignmentType ?? 0);
        var summary =
            "Please confirm:\n" +
            $"Subject: {state.SubjectName}\n" +
            $"Type: {type}\n" +
            $"Title: {state.Title}\n" +
            $"Description: {state.Description ?? "-"}\n" +
            $"Due: {(state.DueAtUtc ?? _clock.UtcNow):yyyy-MM-dd HH:mm} UTC";

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton("Confirm", CallbackData.DeadlineWizardConfirm),
                new InlineButton("Cancel", CallbackData.DeadlineWizardCancel),
            },
        };
        await _telegram.SendKeyboardAsync(chatId, summary, rows, ct);
    }

    private async Task ExecuteAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
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
            var details = string.Join("\n", ex.Errors.Select(e => "- " + e.ErrorMessage));
            await _telegram.SendTextAsync(chatId, $"Could not save the deadline:\n{details}", ct);
            return;
        }

        await _conversations.RemoveAsync(chatId, ct);

        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return;
        }

        var header = state.Flow == ConversationFlow.DeadlineEdit ? "Deadline updated." : "Deadline added.";
        await _telegram.SendTextAsync(chatId, $"{header}\n\n{RenderDeadline(result.Value)}", ct);
    }

    // --- Rendering & helpers --- //

    private static bool IsDeadlineFlow(ConversationState state) =>
        state.Flow == ConversationFlow.DeadlineAdd || state.Flow == ConversationFlow.DeadlineEdit;

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

    private static List<IReadOnlyList<InlineButton>> WithCancel(List<IReadOnlyList<InlineButton>> rows)
    {
        var copy = new List<IReadOnlyList<InlineButton>>(rows)
        {
            new[] { new InlineButton("Cancel", CallbackData.DeadlineWizardCancel) },
        };
        return copy;
    }

    private static List<IReadOnlyList<InlineButton>> SkipCancelRows() => new()
    {
        new[]
        {
            new InlineButton("Skip", CallbackData.DeadlineWizardSkip),
            new InlineButton("Cancel", CallbackData.DeadlineWizardCancel),
        },
    };

    private static List<IReadOnlyList<InlineButton>> CancelRows() => new()
    {
        new[] { new InlineButton("Cancel", CallbackData.DeadlineWizardCancel) },
    };

    private static (DateTime? From, DateTime? To, int PageSize, bool Paged, string Title) ResolveScope(string scope, DateTime nowUtc) => scope switch
    {
        ScopeToday => (nowUtc, nowUtc.Date.AddDays(1), ViewPageSize, true, "Today's events"),
        ScopeWeek => (nowUtc, nowUtc.AddDays(7), ViewPageSize, true, "This week"),
        ScopeNext => (nowUtc, (DateTime?)null, 1, false, "Next deadline"),
        _ => (nowUtc, (DateTime?)null, ViewPageSize, true, "Upcoming deadlines"),
    };

    private static string RenderDeadlinesPage(string title, AssignmentsPageDto page, DateTime nowUtc, bool paged)
    {
        if (page.Items.Count == 0)
        {
            return $"{title}\n\nNo upcoming deadlines.";
        }

        var lines = page.Items.Select((a, i) =>
        {
            var number = paged ? (page.Page - 1) * page.PageSize + i + 1 : i + 1;
            return
                $"{number}. {a.Title} ({a.Type}) — {a.SubjectName}\n" +
                $"   Due: {a.DueAtUtc:yyyy-MM-dd HH:mm} UTC ({FormatRemaining(a.DueAtUtc, nowUtc)})";
        });

        var body = string.Join("\n", lines);
        var footer = paged ? $"\n\nPage {page.Page}/{page.TotalPages}" : string.Empty;
        return $"{title}\n\n{body}{footer}";
    }

    private static string RenderDeadline(AssignmentDto a) =>
        $"Subject: {a.SubjectName}\n" +
        $"Type: {a.Type}\n" +
        $"Title: {a.Title}\n" +
        $"Description: {a.Description ?? "-"}\n" +
        $"Due: {a.DueAtUtc:yyyy-MM-dd HH:mm} UTC";

    private static string FormatRemaining(DateTime dueUtc, DateTime nowUtc)
    {
        var span = dueUtc - nowUtc;
        if (span < TimeSpan.Zero)
        {
            return "overdue";
        }

        if (span.TotalDays >= 1)
        {
            return $"in {(int)span.TotalDays}d";
        }

        if (span.TotalHours >= 1)
        {
            return $"in {(int)span.TotalHours}h";
        }

        return $"in {(int)span.TotalMinutes}m";
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
