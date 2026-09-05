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
        MorningDigestEnabled = true;
        Reminder24hEnabled = true;
        Reminder2hEnabled = true;
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
    public bool MorningDigestEnabled { get; private set; } = true;
    public bool Reminder24hEnabled { get; private set; } = true;
    public bool Reminder2hEnabled { get; private set; } = true;
    public int? QuietHoursStart { get; private set; }
    public int? QuietHoursEnd { get; private set; }

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

    /// <summary>Changes the user's role (administrator action).</summary>
    public void ChangeRole(UserRole role, DateTime nowUtc)
    {
        Role = role;
        UpdatedAt = nowUtc;
    }

    /// <summary>Enables or disables non-critical notifications for this user.</summary>
    public void SetNotificationsEnabled(bool enabled, DateTime nowUtc)
    {
        IsNotificationsEnabled = enabled;
        UpdatedAt = nowUtc;
    }

    /// <summary>Sets the preferred display time zone (IANA id).</summary>
    public void SetTimeZone(string timeZone, DateTime nowUtc)
    {
        TimeZone = timeZone;
        UpdatedAt = nowUtc;
    }

    /// <summary>Sets the preferred language code.</summary>
    public void SetLanguage(string language, DateTime nowUtc)
    {
        Language = language;
        UpdatedAt = nowUtc;
    }

    /// <summary>Enables or disables the daily morning digest.</summary>
    public void SetMorningDigestEnabled(bool enabled, DateTime nowUtc)
    {
        MorningDigestEnabled = enabled;
        UpdatedAt = nowUtc;
    }

    /// <summary>Enables or disables the 24-hours-ahead deadline reminder.</summary>
    public void SetReminder24hEnabled(bool enabled, DateTime nowUtc)
    {
        Reminder24hEnabled = enabled;
        UpdatedAt = nowUtc;
    }

    /// <summary>Enables or disables the 2-hours-ahead deadline reminder.</summary>
    public void SetReminder2hEnabled(bool enabled, DateTime nowUtc)
    {
        Reminder2hEnabled = enabled;
        UpdatedAt = nowUtc;
    }

    /// <summary>Sets custom quiet hours, or clears them (both null) to use the global default.</summary>
    public void SetQuietHours(int? startHour, int? endHour, DateTime nowUtc)
    {
        QuietHoursStart = startHour;
        QuietHoursEnd = endHour;
        UpdatedAt = nowUtc;
    }
}
