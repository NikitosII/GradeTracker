using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Users;

namespace EduTrack.Application.Admin.Commands.ChangeUserRole;

/// <summary>Grants or revokes the administrator role for a target user.</summary>
public sealed record ChangeUserRoleCommand(
    long CallerTelegramUserId,
    Guid TargetUserId,
    UserRole NewRole) : ICommand<AdminUserDto>;
