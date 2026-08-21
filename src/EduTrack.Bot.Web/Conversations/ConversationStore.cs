using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace EduTrack.Bot.Web.Conversations;

/// <summary>
/// Conversation storage.
/// </summary>
public sealed class ConversationStore : IConversationStore
{
    private static readonly DistributedCacheEntryOptions EntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(15),
    };

    private readonly IDistributedCache _cache;

    public ConversationStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    private static string Key(long chatId) => $"conv:{chatId}";

    public async Task<ConversationState?> GetAsync(long chatId, CancellationToken cancellationToken = default)
    {
        var json = await _cache.GetStringAsync(Key(chatId), cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<ConversationState>(json);
    }

    public Task SetAsync(long chatId, ConversationState state, CancellationToken cancellationToken = default)
        => _cache.SetStringAsync(Key(chatId), JsonSerializer.Serialize(state), EntryOptions, cancellationToken);

    public Task RemoveAsync(long chatId, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(Key(chatId), cancellationToken);
}
