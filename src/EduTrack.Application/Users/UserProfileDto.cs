namespace EduTrack.Application.Users;

public sealed record UserProfileDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string? FullName,
    string Role,
    string TimeZone,
    string Language,
    bool IsNotificationsEnabled,
    DateTime CreatedAt);
