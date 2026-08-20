namespace EduTrack.Domain.Users;

/// <summary>
/// A single-use code that grants a role.
/// </summary>
public class InviteCode
{
    private InviteCode()
    {
    }

    private InviteCode(
        Guid id,
        string code,
        UserRole role,
        DateTime? expiresAt,
        Guid? groupId,
        DateTime nowUtc)
    {
        Id = id;
        Code = code;
        Role = role;
        ExpiresAt = expiresAt;
        GroupId = groupId;
        CreatedAt = nowUtc;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public Guid? GroupId { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public Guid? UsedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public bool IsUsed => UsedByUserId is not null;

    public bool IsExpired(DateTime nowUtc) => ExpiresAt is not null && ExpiresAt.Value <= nowUtc;

    /// <summary>True when the code has neither been used nor expired.</summary>
    public bool CanBeRedeemed(DateTime nowUtc) => !IsUsed && !IsExpired(nowUtc);

    public void Redeem(Guid userId)
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("Invite code has already been used.");
        }

        UsedByUserId = userId;
    }

    public static InviteCode Create(
        string code,
        UserRole role,
        DateTime nowUtc,
        DateTime? expiresAt = null,
        Guid? groupId = null)
        => new(Guid.NewGuid(), code, role, expiresAt, groupId, nowUtc);
}
