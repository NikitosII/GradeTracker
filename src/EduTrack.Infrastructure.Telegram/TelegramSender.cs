using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Abstractions.Telegram;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EduTrack.Infrastructure.Telegram;

public sealed class TelegramSender : ITelegramSender
{
    private readonly ITelegramBotClient _botClient;
    private readonly IApplicationMetrics _metrics;

    public TelegramSender(ITelegramBotClient botClient, IApplicationMetrics metrics)
    {
        _botClient = botClient;
        _metrics = metrics;
    }

    public Task SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default)
        => _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);

    public async Task<int> SendKeyboardAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default)
    {
        var message = await _botClient.SendMessage(chatId, text, replyMarkup: ToMarkup(rows), cancellationToken: cancellationToken);
        return message.MessageId;
    }

    public async Task EditKeyboardAsync(long chatId, int messageId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default)
    {
        try
        {
            await _botClient.EditMessageText(chatId, messageId, text, replyMarkup: ToMarkup(rows), cancellationToken: cancellationToken);
        }
        catch (ApiRequestException)
        {
            // Message gone, too old, or unchanged — non-fatal for the wizard flow.
        }
    }

    public async Task DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _botClient.DeleteMessage(chatId, messageId, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException)
        {
            // Already deleted, older than 48h, or no permission — best-effort.
        }
    }

    public async Task AnswerCallbackAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _botClient.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException)
        {
            // Query too old or already answered.
        }
    }

    public async Task SendDocumentAsync(long chatId, string fileName, byte[] content, string? caption = null, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await _botClient.SendDocument(chatId, InputFile.FromStream(stream, fileName), caption: caption, cancellationToken: cancellationToken);
    }

    private static InlineKeyboardMarkup? ToMarkup(IReadOnlyList<IReadOnlyList<InlineButton>> rows)
        => rows.Count == 0
            ? null
            : new InlineKeyboardMarkup(rows.Select(row => row.Select(b => InlineKeyboardButton.WithCallbackData(b.Text, b.CallbackData))));

    public async Task<int> SendNotificationAsync(
        long chatId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons = null,
        CancellationToken cancellationToken = default)
    {
        InlineKeyboardMarkup? markup = buttons is { Count: > 0 }
            ? new InlineKeyboardMarkup(buttons.Select(row => row.Select(b => InlineKeyboardButton.WithCallbackData(b.Text, b.CallbackData))))
            : null;

        try
        {
            var message = await _botClient.SendMessage(chatId, text, replyMarkup: markup, cancellationToken: cancellationToken);
            return message.MessageId;
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 429)
        {
            _metrics.TelegramRateLimited();
            throw;
        }
    }
}
