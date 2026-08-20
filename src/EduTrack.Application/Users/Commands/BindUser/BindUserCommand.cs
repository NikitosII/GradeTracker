using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Users.Commands.BindUser;

/// <summary>
/// Links a Telegram account to a new user by redeeming an invite code
/// </summary>
public sealed record BindUserCommand(
    long TelegramUserId,
    string? Username,
    string? FirstName,
    string? LastName,
    string Code) : ICommand<UserProfileDto>;
