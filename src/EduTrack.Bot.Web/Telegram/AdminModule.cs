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
using EduTrack.Application.Users.Queries.GetUserProfile;
using EduTrack.Bot.Web.Conversations;
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

    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly IConversationStore _conversations;
    private readonly ILogger<AdminModule> _logger;

    public AdminModule(
        ISender sender,
        ITelegramSender telegram,
        IConversationStore conversations,
        ILogger<AdminModule> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _conversations = conversations;
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
            new[] { new InlineButton("Users", CallbackData.AdminUsers(1)) },
            new[] { new InlineButton("Invite Codes", CallbackData.AdminInvites) },
            new[] { new InlineButton("Subjects", CallbackData.AdminSubjects) },
            new[] { new InlineButton("Audit", CallbackData.AdminAudit(1)) },
            new[] { new InlineButton("System Status", CallbackData.AdminStatus) },
        };

        await _telegram.SendKeyboardAsync(chatId, "Admin menu\nChoose a section:", rows, ct);
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
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
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
            var label = $"{DisplayName(user.FullName, user.Username)}: → {targetRole}";
            rows.Add(new[] { new InlineButton(label, CallbackData.AdminSetRole(user.Id, (int)targetRole)) });
        }

        var nav = new List<InlineButton>();
        if (data.HasPrevious)
        {
            nav.Add(new InlineButton("◀ Prev", CallbackData.AdminUsers(page - 1)));
        }

        if (data.HasNext)
        {
            nav.Add(new InlineButton("Next ▶", CallbackData.AdminUsers(page + 1)));
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
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return;
        }

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[] { new InlineButton("➕ New code", CallbackData.AdminNewCode) },
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
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return;
        }

        var rows = new List<IReadOnlyList<InlineButton>>();
        foreach (var subject in result.Value)
        {
            var toggle = subject.IsActive ? "⏻ Deactivate" : "⏻ Activate";
            rows.Add(new[]
            {
                new InlineButton($"✏ {subject.Name}", CallbackData.AdminSubjectRename(subject.Id)),
                new InlineButton(toggle, CallbackData.AdminSubjectToggle(subject.Id)),
            });
        }

        rows.Add(new[] { new InlineButton("➕ New subject", CallbackData.AdminNewSubject) });
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
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return;
        }

        var data = result.Value;
        var rows = new List<IReadOnlyList<InlineButton>>();

        var nav = new List<InlineButton>();
        if (data.HasPrevious)
        {
            nav.Add(new InlineButton("◀ Prev", CallbackData.AdminAudit(page - 1)));
        }

        if (data.HasNext)
        {
            nav.Add(new InlineButton("Next ▶", CallbackData.AdminAudit(page + 1)));
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
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
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
            await _telegram.SendTextAsync(chatId, "Usage: /announce <message>\nBroadcasts a message to every user.", ct);
            return;
        }

        try
        {
            var result = await _sender.Send(new SendAnnouncementCommand(telegramUserId, text), ct);
            if (result.IsFailure)
            {
                await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
                return;
            }

            await _telegram.SendTextAsync(chatId, $"Announcement queued for {result.Value} user(s).", ct);
        }
        catch (ValidationException ex)
        {
            await _telegram.SendTextAsync(chatId, ValidationText(ex), ct);
        }
    }

    public async Task CancelAsync(long chatId, CancellationToken ct)
    {
        await _conversations.RemoveAsync(chatId, ct);
        await _telegram.SendTextAsync(chatId, "Cancelled.", ct);
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
                await _telegram.SendKeyboardAsync(chatId, "Please send a non-empty subject name, or tap Cancel.", CancelRows(), ct);
                return true;

            case AdminStep.SubjectDescription:
                state.Description = text.Trim();
                await CreateSubjectAsync(chatId, telegramUserId, state, ct);
                return true;

            case AdminStep.SubjectNewName when !string.IsNullOrWhiteSpace(text):
                await RenameSubjectAsync(chatId, telegramUserId, state, text.Trim(), ct);
                return true;

            case AdminStep.SubjectNewName:
                await _telegram.SendKeyboardAsync(chatId, "Please send a non-empty name, or tap Cancel.", CancelRows(), ct);
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
                await HandleWizardCallbackAsync(chatId, telegramUserId, parts, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle admin callback {Data} from {TelegramUserId}", data, telegramUserId);
            await _telegram.SendTextAsync(chatId, "Something went wrong. Please try again.", ct);
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

    private async Task HandleWizardCallbackAsync(long chatId, long telegramUserId, string[] parts, CancellationToken ct)
    {
        var action = parts.Length > 1 ? parts[1] : string.Empty;

        switch (action)
        {
            case "x":
                await CancelAsync(chatId, ct);
                break;

            case "role" when parts.Length >= 4 && Guid.TryParse(parts[2], out var userId) && TryParseRole(parts[3], out var role):
                await ChangeRoleAsync(chatId, telegramUserId, userId, role, ct);
                break;

            case "newcode":
                await ShowCodeRolePickerAsync(chatId, ct);
                break;

            case "coderole" when parts.Length >= 3 && TryParseRole(parts[2], out var codeRole):
                await ShowCodeExpiryPickerAsync(chatId, codeRole, ct);
                break;

            case "codeexp" when parts.Length >= 4 && TryParseRole(parts[2], out var expRole) && int.TryParse(parts[3], out var days):
                await CreateCodeAsync(chatId, telegramUserId, expRole, days, ct);
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
                await _telegram.SendTextAsync(chatId, "Please use the buttons above.", ct);
                break;
        }
    }

    private async Task ChangeRoleAsync(long chatId, long telegramUserId, Guid targetUserId, UserRole role, CancellationToken ct)
    {
        var result = await _sender.Send(new ChangeUserRoleCommand(telegramUserId, targetUserId, role), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return;
        }

        var user = result.Value;
        await _telegram.SendTextAsync(chatId, $"Role updated: {DisplayName(user.FullName, user.Username)} is now {user.Role}.", ct);
        await ShowUsersAsync(chatId, telegramUserId, 1, ct);
    }

    private async Task ShowCodeRolePickerAsync(long chatId, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton("Student", CallbackData.AdminCodeRole((int)UserRole.Student)),
                new InlineButton("Admin", CallbackData.AdminCodeRole((int)UserRole.Admin)),
            },
            CancelRow(),
        };
        await _telegram.SendKeyboardAsync(chatId, "New invite code.\nWhich role should it grant?", rows, ct);
    }

    private async Task ShowCodeExpiryPickerAsync(long chatId, UserRole role, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton("1 day", CallbackData.AdminCodeExpiry((int)role, 1)),
                new InlineButton("7 days", CallbackData.AdminCodeExpiry((int)role, 7)),
                new InlineButton("30 days", CallbackData.AdminCodeExpiry((int)role, 30)),
            },
            new[] { new InlineButton("Never", CallbackData.AdminCodeExpiry((int)role, 0)) },
            CancelRow(),
        };
        await _telegram.SendKeyboardAsync(chatId, $"Role: {role}.\nWhen should the code expire?", rows, ct);
    }

    private async Task CreateCodeAsync(long chatId, long telegramUserId, UserRole role, int days, CancellationToken ct)
    {
        int? expiresInDays = days <= 0 ? null : days;

        try
        {
            var result = await _sender.Send(new CreateInviteCodeCommand(telegramUserId, role, expiresInDays), ct);
            if (result.IsFailure)
            {
                await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
                return;
            }

            var code = result.Value;
            var expiry = code.ExpiresAt is { } e ? $"expires {e:yyyy-MM-dd HH:mm} UTC" : "no expiry";
            await _telegram.SendTextAsync(chatId, $"Invite code created:\n\n{code.Code}\nRole: {code.Role}\n{expiry}", ct);
        }
        catch (ValidationException ex)
        {
            await _telegram.SendTextAsync(chatId, ValidationText(ex), ct);
            return;
        }

        await ShowInvitesAsync(chatId, telegramUserId, ct);
    }

    // --- Subject wizards --- //

    private async Task StartSubjectAddAsync(long chatId, CancellationToken ct)
    {
        await _conversations.SetAsync(chatId, new ConversationState
        {
            Flow = ConversationFlow.AdminSubjectAdd,
            Step = AdminStep.SubjectName,
        }, ct);

        await _telegram.SendKeyboardAsync(chatId, "New subject.\nSend the subject name:", CancelRows(), ct);
    }

    private async Task AdvanceToSubjectDescriptionAsync(long chatId, ConversationState state, CancellationToken ct)
    {
        state.Step = AdminStep.SubjectDescription;
        await _conversations.SetAsync(chatId, state, ct);

        var rows = new List<IReadOnlyList<InlineButton>>
        {
            new[]
            {
                new InlineButton("Skip", CallbackData.AdminSubjectSkip),
                new InlineButton("Cancel", CallbackData.AdminCancel),
            },
        };
        await _telegram.SendKeyboardAsync(chatId, "Send a description, or tap Skip.", rows, ct);
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
            await _conversations.RemoveAsync(chatId, ct);

            if (result.IsFailure)
            {
                await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
                return;
            }

            await _telegram.SendTextAsync(chatId, $"Subject added: {result.Value.Name}.", ct);
        }
        catch (ValidationException ex)
        {
            await _conversations.RemoveAsync(chatId, ct);
            await _telegram.SendTextAsync(chatId, ValidationText(ex), ct);
            return;
        }

        await ShowSubjectsAsync(chatId, telegramUserId, ct);
    }

    private async Task StartSubjectRenameAsync(long chatId, long telegramUserId, Guid subjectId, CancellationToken ct)
    {
        var detail = await _sender.Send(new GetSubjectDetailQuery(telegramUserId, subjectId), ct);
        if (detail.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, detail.Error.Message, ct);
            return;
        }

        await _conversations.SetAsync(chatId, new ConversationState
        {
            Flow = ConversationFlow.AdminSubjectRename,
            Step = AdminStep.SubjectNewName,
            SubjectId = subjectId,
        }, ct);

        await _telegram.SendKeyboardAsync(chatId, $"Rename \"{detail.Value.Name}\".\nSend the new name:", CancelRows(), ct);
    }

    private async Task RenameSubjectAsync(long chatId, long telegramUserId, ConversationState state, string newName, CancellationToken ct)
    {
        var subjectId = state.SubjectId ?? Guid.Empty;

        var detail = await _sender.Send(new GetSubjectDetailQuery(telegramUserId, subjectId), ct);
        if (detail.IsFailure)
        {
            await _conversations.RemoveAsync(chatId, ct);
            await _telegram.SendTextAsync(chatId, detail.Error.Message, ct);
            return;
        }

        try
        {
            var result = await _sender.Send(
                new UpdateSubjectCommand(telegramUserId, subjectId, newName, detail.Value.Description, detail.Value.IsActive), ct);
            await _conversations.RemoveAsync(chatId, ct);

            if (result.IsFailure)
            {
                await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
                return;
            }

            await _telegram.SendTextAsync(chatId, $"Subject renamed to {result.Value.Name}.", ct);
        }
        catch (ValidationException ex)
        {
            await _conversations.RemoveAsync(chatId, ct);
            await _telegram.SendTextAsync(chatId, ValidationText(ex), ct);
            return;
        }

        await ShowSubjectsAsync(chatId, telegramUserId, ct);
    }

    private async Task ToggleSubjectAsync(long chatId, long telegramUserId, Guid subjectId, CancellationToken ct)
    {
        var detail = await _sender.Send(new GetSubjectDetailQuery(telegramUserId, subjectId), ct);
        if (detail.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, detail.Error.Message, ct);
            return;
        }

        var d = detail.Value;
        var result = await _sender.Send(
            new UpdateSubjectCommand(telegramUserId, subjectId, d.Name, d.Description, !d.IsActive), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return;
        }

        var state = result.Value.IsActive ? "activated" : "deactivated";
        await _telegram.SendTextAsync(chatId, $"Subject {result.Value.Name} {state}.", ct);
        await ShowSubjectsAsync(chatId, telegramUserId, ct);
    }

    // --- Helpers --- //

    private async Task<bool> EnsureAdminAsync(long chatId, long telegramUserId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserProfileQuery(telegramUserId), ct);
        if (result.IsFailure)
        {
            await _telegram.SendTextAsync(chatId, result.Error.Message, ct);
            return false;
        }

        if (result.Value.Role != nameof(UserRole.Admin))
        {
            await _telegram.SendTextAsync(chatId, AdminErrors.NotAdmin.Message, ct);
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

    private static string DisplayName(string? fullName, string? username)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName!;
        }

        return string.IsNullOrWhiteSpace(username) ? "(no name)" : "@" + username;
    }

    private static string ValidationText(ValidationException ex) =>
        "Invalid input:\n" + string.Join("\n", ex.Errors.Select(e => "- " + e.ErrorMessage));

    private static List<IReadOnlyList<InlineButton>> CancelRows() => new()
    {
        CancelRow(),
    };

    private static IReadOnlyList<InlineButton> CancelRow() => new[]
    {
        new InlineButton("Cancel", CallbackData.AdminCancel),
    };

    private static IReadOnlyList<InlineButton> MenuRow() => new[]
    {
        new InlineButton("⬅ Menu", CallbackData.AdminMenu),
    };

    // --- Rendering --- //

    private static string RenderUsers(AdminUsersPageDto page)
    {
        if (page.Items.Count == 0)
        {
            return "Users\n\nNo users yet.";
        }

        var lines = page.Items.Select((u, i) =>
        {
            var number = (page.Page - 1) * page.PageSize + i + 1;
            var name = DisplayName(u.FullName, u.Username);
            return $"{number}. {name} — {u.Role} (tg:{u.TelegramUserId})";
        });

        return $"Users ({page.TotalCount})\n\n{string.Join("\n", lines)}\n\nPage {page.Page}/{page.TotalPages}\nTap a user to change their role.";
    }

    private static string RenderInvites(IReadOnlyList<InviteCodeDto> codes)
    {
        if (codes.Count == 0)
        {
            return "Invite codes\n\nNo codes yet. Tap \"New code\" to create one.";
        }

        var lines = codes.Select((c, i) =>
        {
            var status = c.IsUsed ? "used" : "available";
            var expiry = c.ExpiresAt is { } e ? $", expires {e:yyyy-MM-dd}" : string.Empty;
            return $"{i + 1}. {c.Code} — {c.Role}, {status}{expiry}";
        });

        return $"Invite codes\n\n{string.Join("\n", lines)}";
    }

    private static string RenderSubjects(IReadOnlyList<EduTrack.Application.Studies.SubjectDto> subjects)
    {
        if (subjects.Count == 0)
        {
            return "Subjects\n\nNo subjects yet. Tap \"New subject\" to add one.";
        }

        var lines = subjects.Select((s, i) => $"{i + 1}. {s.Name} — {(s.IsActive ? "active" : "inactive")}");
        return $"Subjects\n\n{string.Join("\n", lines)}";
    }

    private static string RenderAudit(AuditLogPageDto page)
    {
        if (page.Items.Count == 0)
        {
            return "Audit log\n\nNo entries yet.";
        }

        var lines = page.Items.Select(e =>
        {
            var who = e.ActorName ?? "system";
            return $"{e.CreatedAt:yyyy-MM-dd HH:mm} · {who}\n   {e.Action} {e.EntityType}";
        });

        return $"Audit log ({page.TotalCount})\n\n{string.Join("\n", lines)}\n\nPage {page.Page}/{page.TotalPages}";
    }

    private static string RenderStatus(SystemStatusDto s) =>
        "System status\n\n" +
        $"Users: {s.TotalUsers} (admins: {s.Admins}, students: {s.Students})\n" +
        $"Subjects: {s.Subjects} (active: {s.ActiveSubjects})\n" +
        $"Grades: {s.Grades}\n" +
        $"Deadlines: {s.Assignments}\n" +
        $"Invite codes: {s.InviteCodes} (available: {s.UnusedInviteCodes})\n" +
        $"Audit entries: {s.AuditEntries}\n\n" +
        $"As of {s.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC";
}
