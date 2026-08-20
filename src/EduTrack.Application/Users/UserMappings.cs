using EduTrack.Domain.Users;

namespace EduTrack.Application.Users;

internal static class UserMappings
{
    public static UserProfileDto ToProfileDto(this User user) => new(
        user.Id,
        user.TelegramUserId,
        user.Username,
        user.FullName,
        user.Role.ToString(),
        user.TimeZone,
        user.Language,
        user.IsNotificationsEnabled,
        user.CreatedAt);
}
