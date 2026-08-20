using EduTrack.Application.Abstractions.Telegram;
using Telegram.Bot;

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
}
