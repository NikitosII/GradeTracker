using System.Diagnostics;
using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Localization;
using EduTrack.Application.Reminders;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Commands.BindUser;
using EduTrack.Application.Users.Queries.GetUserProfile;
using EduTrack.Bot.Web.Localization;
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
    private readonly SettingsModule _settings;
    private readonly StatsModule _stats;
    private readonly IUiText _text;
    private readonly ILanguageContext _language;
    private readonly IInboxStore _inbox;
    private readonly IApplicationMetrics _metrics;
    private readonly ILogger<WebhookUpdateProcessor> _logger;

    public WebhookUpdateProcessor(ISender sender, ITelegramSender telegram, GradeModule grades, DeadlineModule deadlines, AdminModule admins, ReminderModule reminders, SettingsModule settings, StatsModule stats, IUiText text, ILanguageContext language, IInboxStore inbox, IApplicationMetrics metrics, ILogger<WebhookUpdateProcessor> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _grades = grades;
        _deadlines = deadlines;
        _admins = admins;
        _reminders = reminders;
        _settings = settings;
        _stats = stats;
        _text = text;
        _language = language;
        _inbox = inbox;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task ProcessAsync(Update update, CancellationToken cancellationToken)
    {
        using var _ = LogContext.PushProperty("TelegramUpdateId", update.Id);
        using var activity = EduTrackTelemetry.ActivitySource.StartActivity("telegram.update");
        activity?.SetTag("telegram.update_id", update.Id);
        activity?.SetTag("telegram.update_type", update.Type.ToString());

        // Idempotency guard: register the update before handling it.
        if (!await _inbox.TryRegisterAsync(update.Id, cancellationToken))
        {
            activity?.SetTag("telegram.duplicate", true);
            _logger.LogInformation("Skipping duplicate Telegram update {TelegramUpdateId}", update.Id);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            await ResolveLanguageAsync(update, cancellationToken);

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
            await _inbox.MarkProcessedAsync(update.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            await MarkFailedSafelyAsync(update.Id, ex.Message, cancellationToken);
            throw;
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

    /// <summary>
    /// Resolves the language for this update
    /// </summary>
    private async Task ResolveLanguageAsync(Update update, CancellationToken cancellationToken)
    {
        var from = update.Message?.From ?? update.CallbackQuery?.From;
        if (from is null)
        {
            return;
        }

        var profile = await _sender.Send(new GetUserProfileQuery(from.Id), cancellationToken);
        _language.Language = profile is { IsSuccess: true }
            ? Normalize(profile.Value.Language)
            : Normalize(from.LanguageCode);
    }

    private static string Normalize(string? code)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            if (code.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
            {
                return "ru";
            }

            if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return "en";
            }
        }

        return "ru";
    }

    private async Task MarkFailedSafelyAsync(long updateId, string error, CancellationToken cancellationToken)
    {
        try
        {
            await _inbox.MarkFailedAsync(updateId, error, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record inbox failure for update {TelegramUpdateId}", updateId);
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
            await _admins.HandleCallbackAsync(chatId, userId, callback.Id, data, callback.Message.MessageId, cancellationToken);
        }
        else if (data.StartsWith(ReminderCallback.Namespace + ":", StringComparison.Ordinal))
        {
            await _reminders.HandleCallbackAsync(chatId, userId, callback.Id, data, cancellationToken);
        }
        else if (data.StartsWith(SettingsModule.Namespace + ":", StringComparison.Ordinal))
        {
            await _settings.HandleCallbackAsync(chatId, userId, callback.Id, data, callback.Message.MessageId, cancellationToken);
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

        var deleteUserMessage = false;

        switch (command)
        {
            case "/start":
                await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.Welcome), cancellationToken);
                break;
            case "/help":
                await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.Help), cancellationToken);
                break;
            case "/bind":
                await _telegram.SendTextAsync(chatId, await HandleBindAsync(message, argument, cancellationToken), cancellationToken);
                break;
            case "/profile":
                await _telegram.SendTextAsync(chatId, await HandleProfileAsync(telegramUserId, cancellationToken), cancellationToken);
                break;
            case "/settings":
                await _settings.ShowSettingsAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/grades":
            case "/subjects":
                await _grades.ShowGradesMenuAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/grade_add":
                await _grades.StartAddAsync(chatId, telegramUserId, cancellationToken);
                deleteUserMessage = true;
                break;
            case "/grade_edit":
                await _grades.StartEditAsync(chatId, telegramUserId, cancellationToken);
                deleteUserMessage = true;
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
                deleteUserMessage = true;
                break;
            case "/deadline_edit":
                await _deadlines.StartEditAsync(chatId, telegramUserId, cancellationToken);
                deleteUserMessage = true;
                break;
            case "/export":
                await _deadlines.ExportAsync(chatId, telegramUserId, cancellationToken);
                break;
            case "/stats":
                await _stats.ShowStatsAsync(chatId, telegramUserId, cancellationToken);
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
                deleteUserMessage = true;
                break;
            default:
                var consumedByWizard = await _grades.TryHandleTextAsync(chatId, telegramUserId, message.Text!, cancellationToken)
                    || await _deadlines.TryHandleTextAsync(chatId, telegramUserId, message.Text!, cancellationToken)
                    || await _admins.TryHandleTextAsync(chatId, telegramUserId, message.Text!, cancellationToken);
                if (consumedByWizard)
                {
                    deleteUserMessage = true;
                }
                else
                {
                    await _telegram.SendTextAsync(chatId, _text.Get(TextKeys.Unknown), cancellationToken);
                }

                break;
        }

        if (deleteUserMessage)
        {
            await _telegram.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
        }

        _logger.LogInformation("Handled {Command} from Telegram user {TelegramUserId}", command, telegramUserId);
    }

    private async Task<string> HandleBindAsync(Message message, string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return _text.Get(TextKeys.BindUsage);
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
            return _text.Get(TextKeys.BindInvalid, details);
        }

        return result.IsSuccess
            ? _text.Get(TextKeys.BindLinked, RenderProfile(result.Value))
            : _text.Error(result.Error);
    }

    private async Task<string> HandleProfileAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUserProfileQuery(telegramUserId), cancellationToken);
        return result.IsSuccess
            ? RenderProfile(result.Value)
            : _text.Error(result.Error);
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

    private string RenderProfile(UserProfileDto p) =>
        _text.Get(TextKeys.ProfileTitle) + "\n" +
        $"{_text.Get(TextKeys.ProfileName)}: {p.FullName ?? "-"}\n" +
        $"{_text.Get(TextKeys.ProfileUsername)}: {(p.Username is null ? "-" : "@" + p.Username)}\n" +
        $"{_text.Get(TextKeys.ProfileRole)}: {p.Role}\n" +
        $"{_text.Get(TextKeys.ProfileTimeZone)}: {p.TimeZone}\n" +
        $"{_text.Get(TextKeys.ProfileLanguage)}: {p.Language}";
}
