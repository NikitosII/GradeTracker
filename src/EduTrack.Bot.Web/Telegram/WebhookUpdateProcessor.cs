using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Commands.BindUser;
using EduTrack.Application.Users.Queries.GetUserProfile;
using EduTrack.Domain.Common;
using FluentValidation;
using MediatR;
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
    private readonly ILogger<WebhookUpdateProcessor> _logger;

    public WebhookUpdateProcessor(ISender sender, ITelegramSender telegram, GradeModule grades, ILogger<WebhookUpdateProcessor> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _grades = grades;
        _logger = logger;
    }

    public async Task ProcessAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery is { } callback)
        {
            await HandleCallbackAsync(callback, cancellationToken);
            return;
        }

        if (update.Message is { } message && message.From is not null && !string.IsNullOrWhiteSpace(message.Text))
        {
            await HandleMessageAsync(message, cancellationToken);
            return;
        }

        _logger.LogDebug("Ignoring update {UpdateId} of type {UpdateType}", update.Id, update.Type);
    }

    private async Task HandleCallbackAsync(CallbackQuery callback, CancellationToken cancellationToken)
    {
        if (callback.Message is null || string.IsNullOrEmpty(callback.Data))
        {
            await _telegram.AnswerCallbackAsync(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        await _grades.HandleCallbackAsync(
            callback.Message.Chat.Id, callback.From.Id, callback.Id, callback.Data, cancellationToken);
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var telegramUserId = message.From!.Id;
        var (command, argument) = ParseCommand(message.Text!);

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
            case "/cancel":
                await _grades.CancelAsync(chatId, cancellationToken);
                break;
            default:
                if (!await _grades.TryHandleTextAsync(chatId, telegramUserId, message.Text!, cancellationToken))
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
            "/cancel - cancel the current action";

        public const string Unknown =
            "Unknown command. Type /help to see what's available.";
    }
}
