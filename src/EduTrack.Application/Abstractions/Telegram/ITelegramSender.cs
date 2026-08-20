namespace EduTrack.Application.Abstractions.Telegram;

public interface ITelegramSender
{
    Task SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default);
}
