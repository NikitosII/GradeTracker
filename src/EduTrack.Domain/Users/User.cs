namespace EduTrack.Domain.Users;

public class User
{
    public const string DefaultTimeZone = "UTC";
    public const string DefaultLanguage = "ru";

    private User()
    {
    }

    private User(
        Guid id,
        long telegramUserId,
        string? username,
        string? firstName,
        string? lastName,
        UserRole role,
        DateTime nowUtc)
    {
        Id = id;
        TelegramUserId = telegramUserId;
        Username = username;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        TimeZone = DefaultTimeZone;
        Language = DefaultLanguage;
        IsNotificationsEnabled = true;
        CreatedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public Guid Id { get; private set; }
    public long TelegramUserId { get; private set; }
    public string? Username { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public UserRole Role { get; private set; }
    public string TimeZone { get; private set; } = DefaultTimeZone;
    public string Language { get; private set; } = DefaultLanguage;
    public bool IsNotificationsEnabled { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Full display name assembled from first/last name.</summary>
    public string? FullName =>
        string.Join(' ', new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)))
            is { Length: > 0 } name
            ? name
            : null;

    public static User Register(
        long telegramUserId,
        string? username,
        string? firstName,
        string? lastName,
        UserRole role,
        DateTime nowUtc)
        => new(Guid.NewGuid(), telegramUserId, username, firstName, lastName, role, nowUtc);
}
