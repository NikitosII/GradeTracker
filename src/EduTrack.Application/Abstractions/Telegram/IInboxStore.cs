namespace EduTrack.Application.Abstractions.Telegram;

/// <summary>
/// Idempotency guard for incoming Telegram updates
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// Records the update as received.
    /// </summary>
    Task<bool> TryRegisterAsync(long updateId, CancellationToken cancellationToken = default);

    /// <summary>Marks a previously registered update as successfully processed.</summary>
    Task MarkProcessedAsync(long updateId, CancellationToken cancellationToken = default);

    /// <summary>Records that processing of a registered update failed.</summary>
    Task MarkFailedAsync(long updateId, string error, CancellationToken cancellationToken = default);
}
