using EduTrack.Application.Abstractions.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace EduTrack.Infrastructure.Telegram;

public sealed class TelegramSender : ITelegramSender
{
    private readonly ITelegramBotClient _botClient;

    public TelegramSender(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public Task SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default)
        => _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);

    public Task SendKeyboardAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default)
    {
        var markup = new InlineKeyboardMarkup(
            rows.Select(row => row.Select(b => InlineKeyboardButton.WithCallbackData(b.Text, b.CallbackData))));

        return _botClient.SendMessage(chatId, text, replyMarkup: markup, cancellationToken: cancellationToken);
    }

    public Task AnswerCallbackAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
        => _botClient.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: cancellationToken);

    public async Task<int> SendNotificationAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        var message = await _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);
        return message.MessageId;
    }
}
