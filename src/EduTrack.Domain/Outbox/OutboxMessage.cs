namespace EduTrack.Domain.Outbox;

/// <summary>
/// A domain message persisted in the same transaction as the state change that produced it.
/// </summary>
public class OutboxMessage
{
    public const int MaxTypeLength = 256;

    private OutboxMessage()
    {
    }

    private OutboxMessage(Guid id, string type, string payload, DateTime occurredAtUtc)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? Error { get; private set; }

    public static OutboxMessage Create(string type, string payload, DateTime nowUtc)
        => new(Guid.NewGuid(), type, payload, nowUtc);

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
