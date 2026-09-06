using System.Collections.Concurrent;
using EduTrack.Application.Abstractions.Telegram;

namespace EduTrack.Integration.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="ITelegramSender"/> that records every outgoing
/// message body instead of calling the Telegram Bot API, so tests can assert on
/// what the bot replied without any network access.
/// </summary>
public sealed class RecordingTelegramSender : ITelegramSender
{
    private int _nextMessageId;

    public ConcurrentQueue<string> Messages { get; } = new();

    /// <summary>Message ids that <see cref="DeleteMessageAsync"/> was asked to remove.</summary>
    public ConcurrentQueue<int> Deleted { get; } = new();

    /// <summary>Documents that <see cref="SendDocumentAsync"/> was asked to send.</summary>
    public ConcurrentQueue<(string FileName, byte[] Content)> Documents { get; } = new();

    public Task SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(text);
        return Task.CompletedTask;
    }

    public Task<int> SendKeyboardAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(text);
        return Task.FromResult(Interlocked.Increment(ref _nextMessageId));
    }

    public Task EditKeyboardAsync(long chatId, int messageId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default)
    {
        // Record the edited body too, so assertions on what the bot "said" hold
        // whether a step was sent fresh or edited into the wizard message.
        Messages.Enqueue(text);
        return Task.CompletedTask;
    }

    public Task DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
    {
        Deleted.Enqueue(messageId);
        return Task.CompletedTask;
    }

    public Task AnswerCallbackAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendDocumentAsync(long chatId, string fileName, byte[] content, string? caption = null, CancellationToken cancellationToken = default)
    {
        Documents.Enqueue((fileName, content));
        if (!string.IsNullOrEmpty(caption))
        {
            Messages.Enqueue(caption);
        }

        return Task.CompletedTask;
    }

    public Task<int> SendNotificationAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>>? buttons = null, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(text);
        return Task.FromResult(1);
    }

    public bool Sent(string substring)
        => Messages.Any(m => m.Contains(substring, StringComparison.OrdinalIgnoreCase));
}
