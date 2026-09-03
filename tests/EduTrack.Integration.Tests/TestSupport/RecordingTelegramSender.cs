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
    public ConcurrentQueue<string> Messages { get; } = new();

    public Task SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(text);
        return Task.CompletedTask;
    }

    public Task SendKeyboardAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>> rows, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(text);
        return Task.CompletedTask;
    }

    public Task AnswerCallbackAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> SendNotificationAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>>? buttons = null, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(text);
        return Task.FromResult(1);
    }

    public bool Sent(string substring)
        => Messages.Any(m => m.Contains(substring, StringComparison.OrdinalIgnoreCase));
}
