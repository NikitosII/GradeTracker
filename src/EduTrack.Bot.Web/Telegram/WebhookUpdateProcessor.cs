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
/// Routes to the matching command/query and replies.
/// </summary>
public sealed class WebhookUpdateProcessor
{
    private readonly ISender _sender;
    private readonly ITelegramSender _telegram;
    private readonly ILogger<WebhookUpdateProcessor> _logger;

    public WebhookUpdateProcessor(ISender sender, ITelegramSender telegram, ILogger<WebhookUpdateProcessor> logger)
    {
        _sender = sender;
        _telegram = telegram;
        _logger = logger;
    }

    public async Task ProcessAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message is not { } message)
        {
            _logger.LogDebug("Ignoring non-message update {UpdateId} of type {UpdateType}",
                update.Id, update.Type);
            return;
        }

        if (message.From is null || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var chatId = message.Chat.Id;
        var (command, argument) = ParseCommand(message.Text);

        var reply = command switch
        {
            "/start" => Text.Welcome,
            "/help" => Text.Help,
            "/bind" => await HandleBindAsync(message, argument, cancellationToken),
            "/profile" => await HandleProfileAsync(message.From.Id, cancellationToken),
            _ => Text.Unknown,
        };

        await _telegram.SendTextAsync(chatId, reply, cancellationToken);
        _logger.LogInformation("Handled {Command} from Telegram user {TelegramUserId}",
            command, message.From.Id);
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
            "/profile - your profile\n\n" +
            "Grades and deadlines arrive in the next stages.";

        public const string Unknown =
            "Unknown command. Type /help to see what's available.";
    }
}
