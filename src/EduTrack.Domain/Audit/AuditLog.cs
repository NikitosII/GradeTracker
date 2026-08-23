namespace EduTrack.Domain.Audit;

/// <summary>
/// An immutable record of a state-changing action, kept for the administrator audit trail.
/// </summary>
public class AuditLog
{
    public const int MaxActionLength = 128;
    public const int MaxEntityTypeLength = 64;
    public const int MaxEntityIdLength = 128;

    private AuditLog()
    {
    }

    private AuditLog(
        Guid id,
        Guid? userId,
        string action,
        string entityType,
        string? entityId,
        string? oldValue,
        string? newValue,
        DateTime nowUtc)
    {
        Id = id;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OldValue = oldValue;
        NewValue = newValue;
        CreatedAt = nowUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>The actor who performed the action, if known.</summary>
    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public string? EntityId { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static AuditLog Create(
        Guid? userId,
        string action,
        string entityType,
        string? entityId,
        string? oldValue,
        string? newValue,
        DateTime nowUtc)
        => new(Guid.NewGuid(), userId, action, entityType, entityId, oldValue, newValue, nowUtc);
}
