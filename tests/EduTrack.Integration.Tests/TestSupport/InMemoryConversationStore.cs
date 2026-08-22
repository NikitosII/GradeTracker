using System.Collections.Concurrent;
using EduTrack.Bot.Web.Conversations;

namespace EduTrack.Integration.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="IConversationStore"/>. The real implementation is
/// backed by Redis via IDistributedCache; this keeps the wizard tests infra-free.
/// </summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<long, ConversationState> _states = new();

    public Task<ConversationState?> GetAsync(long chatId, CancellationToken cancellationToken = default)
        => Task.FromResult(_states.TryGetValue(chatId, out var state) ? state : null);

    public Task SetAsync(long chatId, ConversationState state, CancellationToken cancellationToken = default)
    {
        _states[chatId] = state;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(long chatId, CancellationToken cancellationToken = default)
    {
        _states.TryRemove(chatId, out _);
        return Task.CompletedTask;
    }

    public ConversationState? Peek(long chatId) => _states.TryGetValue(chatId, out var state) ? state : null;
}
