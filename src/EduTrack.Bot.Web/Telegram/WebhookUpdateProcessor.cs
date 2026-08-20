using EduTrack.Application.Abstractions.Telegram;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace EduTrack.Bot.Web.Telegram;

public sealed class WebhookUpdateProcessor
{
    private readonly ITelegramSender _sender;
    private readonly ILogger<WebhookUpdateProcessor> _logger;

    public WebhookUpdateProcessor(ITelegramSender sender, ILogger<WebhookUpdateProcessor> logger)
    {
        _sender = sender;
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

        var chatId = message.Chat.Id;
        var text = message.Text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var reply = text switch
        {
            "/start" =>
                "Welcome to GradeTracker! \n\n" +
                "This bot helps you track your grades, deadlines and GPA.\n" +
                "Account linking and commands are coming in the next stages.\n\n" +
                "Type /help to see what's available.",
            "/help" =>
                "GradeTracker — available now:\n" +
                "/start — getting started\n" +
                "/help — this help\n\n" +
                "More commands (/bind, /grades, /deadlines, ...) arrive in later stages.",
            _ => $"You said: {text}",
        };

        await _sender.SendTextAsync(chatId, reply, cancellationToken);

        _logger.LogInformation("Handled message from chat {ChatId}: {CommandText}", chatId, text);
    }
}
