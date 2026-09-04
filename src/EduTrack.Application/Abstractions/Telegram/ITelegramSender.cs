namespace EduTrack.Application.Abstractions.Telegram;

/// <summary>A single inline-keyboard button.</summary>
public readonly record struct InlineButton(string Text, string CallbackData);

public interface ITelegramSender
{
    Task SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default);

    /// <summary>Sends a keyboard message and returns its Telegram message id.</summary>
    Task<int> SendKeyboardAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits an existing message's text and keyboard in place. 
    /// </summary>
    Task EditKeyboardAsync(long chatId, int messageId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default);

    /// <summary>Deletes a message. Best-effort: failures are swallowed.</summary>
    Task DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken = default);

    Task AnswerCallbackAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default);

    Task<int> SendNotificationAsync(
        long chatId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons = null,
        CancellationToken cancellationToken = default);
}
