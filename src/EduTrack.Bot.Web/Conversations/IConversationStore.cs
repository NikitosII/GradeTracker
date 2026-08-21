namespace EduTrack.Bot.Web.Conversations;

/// <summary>Per-chat storage for in-progress wizard state.</summary>
public interface IConversationStore
{
    Task<ConversationState?> GetAsync(long chatId, CancellationToken cancellationToken = default);

    Task SetAsync(long chatId, ConversationState state, CancellationToken cancellationToken = default);

    Task RemoveAsync(long chatId, CancellationToken cancellationToken = default);
}
