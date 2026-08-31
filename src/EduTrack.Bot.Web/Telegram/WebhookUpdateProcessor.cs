using System.Diagnostics;
using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Reminders;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Commands.BindUser;
using EduTrack.Application.Users.Queries.GetUserProfile;
using EduTrack.Domain.Common;
using EduTrack.Infrastructure.Observability;
using FluentValidation;
using MediatR;
using Serilog.Context;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Routes incoming Telegram updates to the matching command/query and replies.
/// </summary>
public sealed class WebhookUpdateProcessor
{
    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly GradeModule _grades;
    private readonly DeadlineModule _deadlines;
    private readonly AdminModule _admins;
    private readonly ReminderModule _reminders;
    private readonly IApplicationMetrics _metrics;
    private readonly ILogger<WebhookUpdateProcessor> _logger;

    public WebhookUpdateProcessor(ISender sender, ITelegramSender telegram, GradeModule grades, DeadlineModule deadlines, AdminModule admins, ReminderModule reminders, IApplicationMetrics metrics, ILogger<WebhookUpdateProcessor> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _grades = grades;
        _deadlines = deadlines;
        _admins = admins;
        _reminders = reminders;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task ProcessAsync(Update update, CancellationToken cancellationToken)
    {
        using var _ = LogContext.PushProperty("TelegramUpdateId", update.Id);
        using var activity = EduTrackTelemetry.ActivitySource.StartActivity("telegram.update");
        activity?.SetTag("telegram.update_id", update.Id);
        activity?.SetTag("telegram.update_type", update.Type.ToString());

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            if (update.CallbackQuery is { } callback)
            {
                await HandleCallbackAsync(callback, cancellationToken);
            }
            else if (update.Message is { } message && message.From is not null && !string.IsNullOrWhiteSpace(message.Text))
            {
                await HandleMessageAsync(message, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Ignoring update {UpdateId} of type {UpdateType}", update.Id, update.Type);
            }

            success = true;
        }
        finally
        {
            stopwatch.Stop();
            _metrics.WebhookProcessed(success);
            _logger.LogInformation(
                "Processed update {TelegramUpdateId} in {DurationMs} ms (success={Success})",
                update.Id, stopwatch.ElapsedMilliseconds, success);
        }
    }

    private async Task HandleCallbackAsync(CallbackQuery callback, CancellationToken cancellationToken)
    {
        if (callback.Message is null || string.IsNullOrEmpty(callback.Data))
        {
            await _telegram.AnswerCallbackAsync(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        var chatId = callback.Message.Chat.Id;
        var userId = callback.From.Id;
        var data = callback.Data;

        if (data.StartsWith(CallbackData.DeadlineViewNamespace + ":", StringComparison.Ordinal)
            || data.StartsWith(CallbackData.DeadlineWizardNamespace + ":", StringComparison.Ordinal))
        {
            await _deadlines.HandleCallbackAsync(chatId, userId, callback.Id, data, cancellationToken);
        }
        else if (data.StartsWith(CallbackData.AdminViewNamespace + ":", StringComparison.Ordinal)
            || data.StartsWith(CallbackData.AdminWizardNamespace + ":", StringComparison.Ordinal))
        {
            await _admins.HandleCallbackAsync(chatId, userId, callback.Id, data, cancellationToken);
        }
        else if (data.StartsWith(ReminderCallback.Namespace + ":", StringComparison.Ordinal))
        {
            await _reminders.HandleCallbackAsync(chatId, userId, callback.Id, data, cancellationToken);
        }
        else
        {
            await _grades.HandleCallbackAsync(chatId, userId, callback.Id, data, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var telegramUserId = message.From!.Id;
        var (command, argument) = ParseCommand(message.Text!);

        using var commandProperty = LogContext.PushProperty("CommandName", command);
        using var userProperty = LogContext.PushProperty("TelegramUserId", telegramUserId);
        Activity.Current?.SetTag("telegram.command", command);
        Activity.Current?.SetTag("telegram.user_id", telegramUserId);

        switch (command)
        {
            case "/start":
                await _telegram.SendTextAsync(chatId, Text.Welcome, cancellationToken);
                break;
            case "/help":
                await _telegram.SendTextAsync(chatId, Text.Help, cancellationToken);
                break;
            case "/bind":
                await _telegram.SendTextAsync(chatId, await HandleBindAsync(message, argument, cancellationToken), cancellationToken);
                break;
            case "/profile":
                await _telegram.SendTextAsync(chatId, await HandleProfileAsync(telegramUserId, cancellationToken), cancellationToken);
                break;
            case "/grades":
            case "/subjects":
                await _grades.ShowGradesMenuAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/grade_add":
                await _grades.StartAddAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/grade_edit":
                await _grades.StartEditAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/deadlines":
                await _deadlines.ShowDeadlinesAsync(chatId, telegramUserId, DeadlineModule.ScopeAll, 1, cancellationToken);
                break;
            case "/today":
                await _deadlines.ShowDeadlinesAsync(chatId, telegramUserId, DeadlineModule.ScopeToday, 1, cancellationToken);
                break;
            case "/week":
                await _deadlines.ShowDeadlinesAsync(chatId, telegramUserId, DeadlineModule.ScopeWeek, 1, cancellationToken);
                break;
            case "/next":
                await _deadlines.ShowDeadlinesAsync(chatId, telegramUserId, DeadlineModule.ScopeNext, 1, cancellationToken);
                break;
            case "/deadline_add":
                await _deadlines.StartAddAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/deadline_edit":
                await _deadlines.StartEditAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/admin":
                await _admins.ShowMenuAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/users":
                await _admins.ShowUsersAsync(chatId, telegramUserId, 1, cancellationToken);
                break;
            case "/invites":
                await _admins.ShowInvitesAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/audit":
                await _admins.ShowAuditAsync(chatId, telegramUserId, 1, cancellationToken);
                break;
            case "/status":
                await _admins.ShowStatusAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/announce":
                await _admins.SendAnnouncementAsync(chatId, telegramUserId, argument, cancellationToken);
                break;
            case "/cancel":
                await _grades.CancelAsync(chatId, cancellationToken);
                break;
            default:
                if (!await _grades.TryHandleTextAsync(chatId, telegramUserId, message.Text!, cancellationToken)
                    && !await _deadlines.TryHandleTextAsync(chatId, telegramUserId, message.Text!, cancellationToken)
                    && !await _admins.TryHandleTextAsync(chatId, telegramUserId, message.Text!, cancellationToken))
                {
                    await _telegram.SendTextAsync(chatId, Text.Unknown, cancellationToken);
                }

                break;
        }

        _logger.LogInformation("Handled {Command} from Telegram user {TelegramUserId}", command, telegramUserId);
    }

    private async Task<string> HandleBindAsync(Message message, string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "Usage: /bind <code>\nEnter the invite code you received.";
        }

        var from = message.From!;
        var command = new BindUserCommand(from.Id, from.Username, from.FirstName, from.LastName, code);

        Result<UserProfileDto> result;
        try
        {
            result = await _sender.Send(command, cancellationToken);
        }
        catch (ValidationException ex)
        {
            var details = string.Join("\n", ex.Errors.Select(e => "- " + e.ErrorMessage));
            return $"The code is not valid:\n{details}";
        }

        return result.IsSuccess
            ? $"Account linked!\n\n{RenderProfile(result.Value)}"
            : result.Error.Message;
    }

    private async Task<string> HandleProfileAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUserProfileQuery(telegramUserId), cancellationToken);
        return result.IsSuccess
            ? RenderProfile(result.Value)
            : result.Error.Message;
    }

    private static (string Command, string? Argument) ParseCommand(string text)
    {
        var parts = text.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var at = command.IndexOf('@');
        if (at >= 0)
        {
            command = command[..at];
        }

        var argument = parts.Length > 1 ? parts[1].Trim() : null;
        return (command, argument);
    }

    private static string RenderProfile(UserProfileDto p) =>
        "Profile\n" +
        $"Name: {p.FullName ?? "-"}\n" +
        $"Username: {(p.Username is null ? "-" : "@" + p.Username)}\n" +
        $"Role: {p.Role}\n" +
        $"Time zone: {p.TimeZone}\n" +
        $"Language: {p.Language}";

    private static class Text
    {
        public const string Welcome =
            "Welcome to GradeTracker!\n\n" +
            "This bot helps you track your grades, deadlines and GPA.\n" +
            "To get started, link your account with an invite code:\n" +
            "/bind <code>\n\n" +
            "Type /help to see available commands.";

        public const string Help =
            "GradeTracker - available commands:\n" +
            "/start - getting started\n" +
            "/help - this help\n" +
            "/bind <code> - link your account\n" +
            "/profile - your profile\n" +
            "/grades - view your grades by subject\n" +
            "/subjects - list subjects\n" +
            "/grade_add - add a grade\n" +
            "/grade_edit - edit a grade\n" +
            "/deadlines - your upcoming deadlines\n" +
            "/today - deadlines due today\n" +
            "/week - deadlines due this week\n" +
            "/next - your nearest deadline\n" +
            "/deadline_add - add a deadline\n" +
            "/deadline_edit - edit a deadline\n" +
            "/cancel - cancel the current action\n\n" +
            "Admin only:\n" +
            "/admin - admin menu\n" +
            "/users - manage users and roles\n" +
            "/invites - manage invite codes\n" +
            "/audit - view the audit log\n" +
            "/status - system status\n" +
            "/announce <message> - broadcast to all users";

        public const string Unknown =
            "Unknown command. Type /help to see what's available.";
    }
}
