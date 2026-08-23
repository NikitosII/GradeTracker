namespace EduTrack.Application.Admin;

public sealed record AdminUserDto(
    Guid Id,
    long TelegramUserId,
    string? Username,
    string? FullName,
    string Role,
    DateTime CreatedAt);
