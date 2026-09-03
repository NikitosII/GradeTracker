namespace EduTrack.Domain.Inbox;

/// <summary>
/// Records a Telegram update that has been received, so the same update is never
/// processed twice. The webhook consumer is idempotent: it registers the update
/// id before handling it and skips any update already present.
/// </summary>
public class InboxMessage
{
    private InboxMessage()
    {
    }

    private InboxMessage(Guid id, long updateId, DateTime receivedAtUtc)
    {
        Id = id;
        UpdateId = updateId;
        ReceivedAtUtc = receivedAtUtc;
    }

    public Guid Id { get; private set; }
    public long UpdateId { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? Error { get; private set; }

    public static InboxMessage Receive(long updateId, DateTime nowUtc)
        => new(Guid.NewGuid(), updateId, nowUtc);

    public void MarkProcessed(DateTime nowUtc)
    {
        ProcessedAtUtc = nowUtc;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
    }
}
