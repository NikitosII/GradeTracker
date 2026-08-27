namespace EduTrack.Application.Abstractions.Telegram;

/// <summary>A single inline-keyboard button.</summary>
public readonly record struct InlineButton(string Text, string CallbackData);

public interface ITelegramSender
{
    Task SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default);

    Task SendKeyboardAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default);

    Task AnswerCallbackAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default);

    Task<int> SendNotificationAsync(
        long chatId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons = null,
        CancellationToken cancellationToken = default);
}
