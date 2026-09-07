using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Admin;
using EduTrack.Application.Admin.Commands.ChangeUserRole;
using EduTrack.Application.Admin.Commands.CreateInviteCode;
using EduTrack.Application.Admin.Commands.CreateSubject;
using EduTrack.Application.Admin.Commands.SendAnnouncement;
using EduTrack.Application.Admin.Commands.UpdateSubject;
using EduTrack.Application.Admin.Queries.GetAllSubjects;
using EduTrack.Application.Admin.Queries.GetAuditLog;
using EduTrack.Application.Admin.Queries.GetInviteCodes;
using EduTrack.Application.Admin.Queries.GetSubjectDetail;
using EduTrack.Application.Admin.Queries.GetSystemStatus;
using EduTrack.Application.Admin.Queries.GetUsers;
using EduTrack.Application.Localization;
using EduTrack.Application.Users.Queries.GetUserProfile;
using EduTrack.Bot.Web.Conversations;
using EduTrack.Bot.Web.Localization;
using EduTrack.Domain.Users;
using FluentValidation;
using MediatR;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Handles the administrator experience: users, invite codes, subjects, audit log and status.
/// </summary>
public sealed class AdminModule
{
    private const int UsersPageSize = 8;
    private const int AuditPageSize = 8;

    private static readonly IReadOnlyList<IReadOnlyList<InlineButton>> NoKeyboard =
        Array.Empty<IReadOnlyList<InlineButton>>();

    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly IConversationStore _conversations;
    private readonly IUiText _text;
    private readonly ILogger<AdminModule> _logger;

    public AdminModule(
        ISender sender,
        ITelegramSender telegram,
        IConversationStore conversations,
        IUiText text,
        ILogger<AdminModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _conversations = conversations;
        _text = text;
        _logger = logger;
    }

    // --- Entry points from the update router --- //

    public async Task ShowMenuAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(chatId, telegramUserId, ct))
        {
            return;
        }

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[] { new InlineButton(_text.Get(TextKeys.AdminBtnUsers), CallbackData.AdminUsers(1)) },
            new[] { new InlineButton(_text.Get(TextKeys.AdminBtnInvites), CallbackData.AdminInvites) },
            new[] { new InlineButton(_text.Get(TextKeys.AdminBtnSubjects), CallbackData.AdminSubjects) },
            new[] { new InlineButton(_text.Get(TextKeys.AdminBtnAudit), CallbackData.AdminAudit(1)) },
            new[] { new InlineButton(_text.Get(TextKeys.AdminBtnStatus), CallbackData.AdminStatus) },
        };

        await _telegram.SendKeyboardAsync(chatId, _text.Get(TextKeys.AdminMenuTitle), rows, ct);
    }

    public async Task ShowUsersAsync(long chatId, long telegramUserId, int page, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(chatId, telegramUserId, ct))
        {
            return;
        }

        var result = await _sender.Send(new GetUsersQuery(telegramUserId, page, UsersPageSize), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        var data = result.Value;
        var rows = new List<IReadOnlyList<InlineButton>>();

        foreach (var user in data.Items)
        {
            if (user.TelegramUserId == telegramUserId)
            {
                continue; // no self role toggle
            }

            var isAdmin = user.Role == nameof(UserRole.Admin);
            var targetRole = isAdmin ? UserRole.Student : UserRole.Admin;
            var label = $"{DisplayName(user.FullName, user.Username)}: → {RoleLabel(targetRole.ToString())}";
            rows.Add(new[] { new InlineButton(label, CallbackData.AdminSetRole(user.Id, (int)targetRole)) });
        }

        var nav = new List<InlineButton>();
        if (data.HasPrevious)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonPrev), CallbackData.AdminUsers(page - 1)));
        }

        if (data.HasNext)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonNext), CallbackData.AdminUsers(page + 1)));
        }

        if (nav.Count > 0)
        {
            rows.Add(nav);
        }

        rows.Add(MenuRow());

        await _telegram.SendKeyboardAsync(chatId, RenderUsers(data), rows, ct);
    }

    public async Task ShowInvitesAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(chatId, telegramUserId, ct))
        {
            return;
        }

        var result = await _sender.Send(new GetInviteCodesQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[] { new InlineButton(_text.Get(TextKeys.AdminBtnNewCode), CallbackData.AdminNewCode) },
            MenuRow(),
        };

        await _telegram.SendKeyboardAsync(chatId, RenderInvites(result.Value), rows, ct);
    }

    public async Task ShowSubjectsAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(chatId, telegramUserId, ct))
        {
            return;
        }

        var result = await _sender.Send(new GetAllSubjectsQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        var rows = new List<IReadOnlyList<InlineButton>>();
        foreach (var subject in result.Value)
        {
            var toggle = subject.IsActive ? _text.Get(TextKeys.AdminBtnDeactivate) : _text.Get(TextKeys.AdminBtnActivate);
            rows.Add(new[]
            {
                new InlineButton($"✏ {subject.Name}", CallbackData.AdminSubjectRename(subject.Id)),
                new InlineButton(toggle, CallbackData.AdminSubjectToggle(subject.Id)),
            });
        }

        rows.Add(new[] { new InlineButton(_text.Get(TextKeys.AdminBtnNewSubject), CallbackData.AdminNewSubject) });
        rows.Add(MenuRow());

        await _telegram.SendKeyboardAsync(chatId, RenderSubjects(result.Value), rows, ct);
    }

    public async Task ShowAuditAsync(long chatId, long telegramUserId, int page, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(chatId, telegramUserId, ct))
        {
            return;
        }

        var result = await _sender.Send(new GetAuditLogQuery(telegramUserId, page, AuditPageSize), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        var data = result.Value;
        var rows = new List<IReadOnlyList<InlineButton>>();

        var nav = new List<InlineButton>();
        if (data.HasPrevious)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonPrev), CallbackData.AdminAudit(page - 1)));
        }

        if (data.HasNext)
        {
            nav.Add(new InlineButton(_text.Get(TextKeys.CommonNext), CallbackData.AdminAudit(page + 1)));
        }

        if (nav.Count > 0)
        {
            rows.Add(nav);
        }

        rows.Add(MenuRow());

        await _telegram.SendKeyboardAsync(chatId, RenderAudit(data), rows, ct);
    }

    public async Task ShowStatusAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(chatId, telegramUserId, ct))
        {
            return;
        }

        var result = await _sender.Send(new GetSystemStatusQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        await _telegram.SendKeyboardAsync(chatId, RenderStatus(result.Value), new List<IReadOnlyList<InlineButton>> { MenuRow() }, ct);
    }

    public async Task SendAnnouncementAsync(long chatId, long telegramUserId, string? text, CancellationToken ct)
    {
        if (!await EnsureAdminAsync(chatId, telegramUserId, ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.AdminAnnounceUsage), ct);
            return;
        }

        try
        {
            var result = await _sender.Send(new SendAnnouncementCommand(telegramUserId, text), ct);
            if (result.IsFailure)
            {
                await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
                return;
            }

            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.AdminAnnounceQueued, result.Value), ct);
        }
        catch (ValidationException ex)
        {
            await _telegram.SendTextAsync(chatId, ValidationText(ex), ct);
        }
    }

    public async Task CancelAsync(long chatId, int messageId, CancellationToken ct)
    {
        // The Cancel button always sits on the message we want to replace — edit
        // it to the outcome and drop any wizard state (there is none for the
        // stateless code/role pickers).
        await _conversations.RemoveAsync(chatId, ct);
        await _telegram.EditKeyboardAsync(chatId, messageId, _text.Get(TextKeys.CommonCancelled), NoKeyboard, ct);
    }

    /// <summary>Feeds a plain text message into the active admin subject wizard.</summary>
    public async Task<bool> TryHandleTextAsync(long chatId, long telegramUserId, string text, CancellationToken ct)
    {
        var state = await _conversations.GetAsync(chatId, ct);
        if (state is null || !IsAdminFlow(state))
        {
            return false;
        }

        switch (state.Step)
        {
            case AdminStep.SubjectName when !string.IsNullOrWhiteSpace(text):
                state.Title = text.Trim();
                await AdvanceToSubjectDescriptionAsync(chatId, state, ct);
                return true;

            case AdminStep.SubjectName:
                await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.AdminSubjectNameEmpty), CancelRows(), ct);
                return true;

            case AdminStep.SubjectDescription:
                state.Description = text.Trim();
                await CreateSubjectAsync(chatId, telegramUserId, state, ct);
                return true;

            case AdminStep.SubjectNewName when !string.IsNullOrWhiteSpace(text):
                await RenameSubjectAsync(chatId, telegramUserId, state, text.Trim(), ct);
                return true;

            case AdminStep.SubjectNewName:
                await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.AdminSubjectNewNameEmpty), CancelRows(), ct);
                return true;

            default:
                await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonUseButtons), ct);
                return true;
        }
    }

    public async Task HandleCallbackAsync(long chatId, long telegramUserId, string callbackQueryId, string data, int messageId, CancellationToken ct)
    {
        try
        {
            if (!await EnsureAdminAsync(chatId, telegramUserId, ct))
            {
                return;
            }

            var parts = CallbackData.Parts(data);
            var ns = parts.Length > 0 ? parts[0] : string.Empty;

            if (ns == CallbackData.AdminViewNamespace)
            {
                await HandleViewCallbackAsync(chatId, telegramUserId, parts, ct);
            }
            else if (ns == CallbackData.AdminWizardNamespace)
            {
                await HandleWizardCallbackAsync(chatId, telegramUserId, parts, messageId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle admin callback {Data} from {TelegramUserId}", data, telegramUserId);
            await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonSomethingWrong), ct);
        }
        finally
        {
            await _telegram.AnswerCallbackAsync(callbackQueryId, cancellationToken: ct);
        }
    }

    // --- View callbacks --- //

    private async Task HandleViewCallbackAsync(long chatId, long telegramUserId, string[] parts, CancellationToken ct)
    {
        var action = parts.Length > 1 ? parts[1] : string.Empty;

        switch (action)
        {
            case "menu":
                await ShowMenuAsync(chatId, telegramUserId, ct);
                break;
            case "users":
                await ShowUsersAsync(chatId, telegramUserId, ParsePage(parts, 2), ct);
                break;
            case "invites":
                await ShowInvitesAsync(chatId, telegramUserId, ct);
                break;
            case "subjects":
                await ShowSubjectsAsync(chatId, telegramUserId, ct);
                break;
            case "audit":
                await ShowAuditAsync(chatId, telegramUserId, ParsePage(parts, 2), ct);
                break;
            case "status":
                await ShowStatusAsync(chatId, telegramUserId, ct);
                break;
        }
    }

    // --- Wizard / mutating callbacks --- //

    private async Task HandleWizardCallbackAsync(long chatId, long telegramUserId, string[] parts, int messageId, CancellationToken ct)
    {
        var action = parts.Length > 1 ? parts[1] : string.Empty;

        switch (action)
        {
            case "x":
                await CancelAsync(chatId, messageId, ct);
                break;

            case "role" when parts.Length >= 4 && Guid.TryParse(parts[2], out var userId) && TryParseRole(parts[3], out var role):
                await ChangeRoleAsync(chatId, telegramUserId, userId, role, messageId, ct);
                break;

            case "newcode":
                await ShowCodeRolePickerAsync(chatId, messageId, ct);
                break;

            case "coderole" when parts.Length >= 3 && TryParseRole(parts[2], out var codeRole):
                await ShowCodeExpiryPickerAsync(chatId, codeRole, messageId, ct);
                break;

            case "codeexp" when parts.Length >= 4 && TryParseRole(parts[2], out var expRole) && int.TryParse(parts[3], out var days):
                await CreateCodeAsync(chatId, telegramUserId, expRole, days, messageId, ct);
                break;

            case "newsubject":
                await StartSubjectAddAsync(chatId, ct);
                break;

            case "subrename" when parts.Length >= 3 && Guid.TryParse(parts[2], out var renameId):
                await StartSubjectRenameAsync(chatId, telegramUserId, renameId, ct);
                break;

            case "subtoggle" when parts.Length >= 3 && Guid.TryParse(parts[2], out var toggleId):
                await ToggleSubjectAsync(chatId, telegramUserId, toggleId, ct);
                break;

            case "subskip" when await IsAtStepAsync(chatId, AdminStep.SubjectDescription, ct):
                await SkipSubjectDescriptionAsync(chatId, telegramUserId, ct);
                break;

            default:
                await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.CommonUseButtons), ct);
                break;
        }
    }

    private async Task ChangeRoleAsync(long chatId, long telegramUserId, Guid targetUserId, UserRole role, int messageId, CancellationToken ct)
    {
        var result = await _sender.Send(new ChangeUserRoleCommand(telegramUserId, targetUserId, role), ct);
        if (result.IsFailure)
        {
            await _telegram.EditKeyboardAsync(chatId, messageId, _text.Error(result.Error), NoKeyboard, ct);
            return;
        }

        var user = result.Value;
        await _telegram.EditKeyboardAsync(chatId, messageId, _text.Get(TextKeys.AdminRoleUpdated, DisplayName(user.FullName, user.Username), RoleLabel(user.Role)), NoKeyboard, ct);

        // Auto-refresh: re-show the user list with the updated role.
        await ShowUsersAsync(chatId, telegramUserId, 1, ct);
    }

    private async Task ShowCodeRolePickerAsync(long chatId, int messageId, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton(RoleLabel(nameof(UserRole.Student)), CallbackData.AdminCodeRole((int)UserRole.Student)),
                new InlineButton(RoleLabel(nameof(UserRole.Admin)), CallbackData.AdminCodeRole((int)UserRole.Admin)),
            },
            CancelRow(),
        };
        await _telegram.EditKeyboardAsync(chatId, messageId, _text.Get(TextKeys.AdminRolePicker), rows, ct);
    }

    private async Task ShowCodeExpiryPickerAsync(long chatId, UserRole role, int messageId, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton(_text.Get(TextKeys.AdminExpiry1Day), CallbackData.AdminCodeExpiry((int)role, 1)),
                new InlineButton(_text.Get(TextKeys.AdminExpiry7Days), CallbackData.AdminCodeExpiry((int)role, 7)),
                new InlineButton(_text.Get(TextKeys.AdminExpiry30Days), CallbackData.AdminCodeExpiry((int)role, 30)),
            },
            new[] { new InlineButton(_text.Get(TextKeys.AdminExpiryNever), CallbackData.AdminCodeExpiry((int)role, 0)) },
            CancelRow(),
        };
        await _telegram.EditKeyboardAsync(chatId, messageId, _text.Get(TextKeys.AdminExpiryPicker, RoleLabel(role.ToString())), rows, ct);
    }

    private async Task CreateCodeAsync(long chatId, long telegramUserId, UserRole role, int days, int messageId, CancellationToken ct)
    {
        int? expiresInDays = days <= 0 ? null : days;

        try
        {
            var result = await _sender.Send(new CreateInviteCodeCommand(telegramUserId, role, expiresInDays), ct);
            if (result.IsFailure)
            {
                await _telegram.EditKeyboardAsync(chatId, messageId, _text.Error(result.Error), NoKeyboard, ct);
                return;
            }

            var code = result.Value;
            var expiry = code.ExpiresAt is { } e
                ? _text.Get(TextKeys.AdminCodeExpires, e.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture))
                : _text.Get(TextKeys.AdminCodeNoExpiry);
            await _telegram.EditKeyboardAsync(chatId, messageId, _text.Get(TextKeys.AdminCodeCreated, code.Code, RoleLabel(code.Role.ToString()), expiry), NoKeyboard, ct);
        }
        catch (ValidationException ex)
        {
            await _telegram.EditKeyboardAsync(chatId, messageId, ValidationText(ex), NoKeyboard, ct);
            return;
        }

        // Auto-refresh: re-show the invite list with the new code.
        await ShowInvitesAsync(chatId, telegramUserId, ct);
    }

    // --- Subject wizards --- //

    private async Task StartSubjectAddAsync(long chatId, CancellationToken ct)
    {
        var state = new ConversationState
        {
            Flow = ConversationFlow.AdminSubjectAdd,
            Step = AdminStep.SubjectName,
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.AdminNewSubjectPrompt), CancelRows(), ct);
    }

    private async Task AdvanceToSubjectDescriptionAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = AdminStep.SubjectDescription;

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton(_text.Get(TextKeys.CommonSkip), CallbackData.AdminSubjectSkip),
                new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.AdminCancel),
            },
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.AdminSubjectDescPrompt), rows, ct);
    }

    private async Task SkipSubjectDescriptionAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var state = await _conversations.GetAsync(chatId, ct);
        if (state is null || state.Flow != ConversationFlow.AdminSubjectAdd)
        {
            return;
        }

        state.Description = null;
        await CreateSubjectAsync(chatId, telegramUserId, state, ct);
    }

    private async Task CreateSubjectAsync(long chatId, long telegramUserId, ConversationState state, CancellationToken ct)
    {
        var name = state.Title ?? string.Empty;

        try
        {
            var result = await _sender.Send(new CreateSubjectCommand(telegramUserId, name, state.Description), ct);

            if (result.IsFailure)
            {
                await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Error(result.Error), ct);
                return;
            }

            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.AdminSubjectAdded, result.Value.Name), ct);
        }
        catch (ValidationException ex)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, ValidationText(ex), ct);
            return;
        }

        await ShowSubjectsAsync(chatId, telegramUserId, ct);
    }

    private async Task StartSubjectRenameAsync(long chatId, long telegramUserId, Guid subjectId, CancellationToken ct)
    {
        var detail = await _sender.Send(new GetSubjectDetailQuery(telegramUserId, subjectId), ct);
        if (detail.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(detail.Error), ct);
            return;
        }

        var state = new ConversationState
        {
            Flow = ConversationFlow.AdminSubjectRename,
            Step = AdminStep.SubjectNewName,
            SubjectId = subjectId,
        };
        await WizardUi.ShowStepAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.AdminSubjectRenamePrompt, detail.Value.Name), CancelRows(), ct);
    }

    private async Task RenameSubjectAsync(long chatId, long telegramUserId, ConversationState state, string newName, CancellationToken ct)
    {
        var subjectId = state.SubjectId ?? Guid.Empty;

        var detail = await _sender.Send(new GetSubjectDetailQuery(telegramUserId, subjectId), ct);
        if (detail.IsFailure)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Error(detail.Error), ct);
            return;
        }

        try
        {
            var result = await _sender.Send(
                new UpdateSubjectCommand(telegramUserId, subjectId, newName, detail.Value.Description, detail.Value.IsActive), ct);

            if (result.IsFailure)
            {
                await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Error(result.Error), ct);
                return;
            }

            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, _text.Get(TextKeys.AdminSubjectRenamed, result.Value.Name), ct);
        }
        catch (ValidationException ex)
        {
            await WizardUi.CompleteAsync(_telegram, _conversations, chatId, state, ValidationText(ex), ct);
            return;
        }

        await ShowSubjectsAsync(chatId, telegramUserId, ct);
    }

    private async Task ToggleSubjectAsync(long chatId, long telegramUserId, Guid subjectId, CancellationToken ct)
    {
        var detail = await _sender.Send(new GetSubjectDetailQuery(telegramUserId, subjectId), ct);
        if (detail.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(detail.Error), ct);
            return;
        }

        var d = detail.Value;
        var result = await _sender.Send(
            new UpdateSubjectCommand(telegramUserId, subjectId, d.Name, d.Description, !d.IsActive), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return;
        }

        var message = result.Value.IsActive
            ? _text.Get(TextKeys.AdminSubjectActivated, result.Value.Name)
            : _text.Get(TextKeys.AdminSubjectDeactivated, result.Value.Name);
        await _telegram.SendTextAsync(chatId, message, ct);
        await ShowSubjectsAsync(chatId, telegramUserId, ct);
    }

    // --- Helpers --- //

    private async Task<bool> EnsureAdminAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserProfileQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, _text.Error(result.Error), ct);
            return false;
        }

        if (result.Value.Role != nameof(UserRole.Admin))
        {
            await _telegram.SendTextAsync(chatId, _text.Error(AdminErrors.NotAdmin), ct);
            return false;
        }

        return true;
    }

    private async Task<bool> IsAtStepAsync(long chatId, string step, CancellationToken ct)
    {
        var state = await _conversations.GetAsync(chatId, ct);
        return state is not null && IsAdminFlow(state) && state.Step == step;
    }

    private static bool IsAdminFlow(ConversationState state) =>
        state.Flow == ConversationFlow.AdminSubjectAdd || state.Flow == ConversationFlow.AdminSubjectRename;

    private static bool TryParseRole(string text, out UserRole role)
    {
        if (int.TryParse(text, out var raw) && Enum.IsDefined(typeof(UserRole), raw))
        {
            role = (UserRole)raw;
            return true;
        }

        role = default;
        return false;
    }

    private static int ParsePage(string[] parts, int index) =>
        parts.Length > index && int.TryParse(parts[index], out var page) && page > 0 ? page : 1;

    private string RoleLabel(string roleName) => _text.Get(TextKeys.Role(roleName));

    private string DisplayName(string? fullName, string? username)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName!;
        }

        return string.IsNullOrWhiteSpace(username) ? _text.Get(TextKeys.AdminNoName) : "@" + username;
    }

    private string ValidationText(ValidationException ex) =>
        _text.Get(TextKeys.AdminValidation, string.Join("\n", ex.Errors.Select(e => "- " + e.ErrorMessage)));

    private IReadOnlyList<IReadOnlyList<InlineButton>> CancelRows() => new List<IReadOnlyList<InlineButton>>
    {
        CancelRow(),
    };

    private IReadOnlyList<InlineButton> CancelRow() => new[]
    {
        new InlineButton(_text.Get(TextKeys.CommonCancel), CallbackData.AdminCancel),
    };

    private IReadOnlyList<InlineButton> MenuRow() => new[]
    {
        new InlineButton(_text.Get(TextKeys.AdminBtnMenu), CallbackData.AdminMenu),
    };

    // --- Rendering --- //

    private string RenderUsers(AdminUsersPageDto page)
    {
        if (page.Items.Count == 0)
        {
            return _text.Get(TextKeys.AdminUsersEmpty);
        }

        var lines = page.Items.Select((u, i) =>
        {
            var number = (page.Page - 1) * page.PageSize + i + 1;
            var name = DisplayName(u.FullName, u.Username);
            return $"{number}. {name} — {RoleLabel(u.Role)} (tg:{u.TelegramUserId})";
        });

        return $"{_text.Get(TextKeys.AdminUsersTitle, page.TotalCount)}\n\n{string.Join("\n", lines)}\n\n" +
            $"{_text.Get(TextKeys.CommonPage, page.Page, page.TotalPages)}\n{_text.Get(TextKeys.AdminUsersTap)}";
    }

    private string RenderInvites(IReadOnlyList<InviteCodeDto> codes)
    {
        if (codes.Count == 0)
        {
            return _text.Get(TextKeys.AdminInvitesEmpty);
        }

        var lines = codes.Select((c, i) =>
        {
            var status = c.IsUsed ? _text.Get(TextKeys.AdminInviteUsed) : _text.Get(TextKeys.AdminInviteAvailable);
            var expiry = c.ExpiresAt is { } e
                ? _text.Get(TextKeys.AdminInviteExpires, e.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
                : string.Empty;
            return $"{i + 1}. {c.Code} — {RoleLabel(c.Role.ToString())}, {status}{expiry}";
        });

        return $"{_text.Get(TextKeys.AdminInvitesTitle)}\n\n{string.Join("\n", lines)}";
    }

    private string RenderSubjects(IReadOnlyList<EduTrack.Application.Studies.SubjectDto> subjects)
    {
        if (subjects.Count == 0)
        {
            return _text.Get(TextKeys.AdminSubjectsEmpty);
        }

        var lines = subjects.Select((s, i) =>
            $"{i + 1}. {s.Name} — {(s.IsActive ? _text.Get(TextKeys.AdminSubjectActive) : _text.Get(TextKeys.AdminSubjectInactive))}");
        return $"{_text.Get(TextKeys.AdminSubjectsTitle)}\n\n{string.Join("\n", lines)}";
    }

    private string RenderAudit(AuditLogPageDto page)
    {
        if (page.Items.Count == 0)
        {
            return _text.Get(TextKeys.AdminAuditEmpty);
        }

        var lines = page.Items.Select(e =>
        {
            var who = e.ActorName ?? _text.Get(TextKeys.AdminAuditSystem);
            return $"{e.CreatedAt:yyyy-MM-dd HH:mm} · {who}\n   {e.Action} {e.EntityType}";
        });

        return $"{_text.Get(TextKeys.AdminAuditTitle, page.TotalCount)}\n\n{string.Join("\n", lines)}\n\n" +
            $"{_text.Get(TextKeys.CommonPage, page.Page, page.TotalPages)}";
    }

    private string RenderStatus(SystemStatusDto s) =>
        _text.Get(TextKeys.AdminStatusTitle) + "\n\n" +
        _text.Get(TextKeys.AdminStatusUsers, s.TotalUsers, s.Admins, s.Students) + "\n" +
        _text.Get(TextKeys.AdminStatusSubjects, s.Subjects, s.ActiveSubjects) + "\n" +
        _text.Get(TextKeys.AdminStatusGrades, s.Grades) + "\n" +
        _text.Get(TextKeys.AdminStatusDeadlines, s.Assignments) + "\n" +
        _text.Get(TextKeys.AdminStatusInvites, s.InviteCodes, s.UnusedInviteCodes) + "\n" +
        _text.Get(TextKeys.AdminStatusAudit, s.AuditEntries) + "\n\n" +
        _text.Get(TextKeys.AdminStatusAsOf, s.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
}
