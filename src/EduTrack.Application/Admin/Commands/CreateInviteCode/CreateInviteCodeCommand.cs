using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Users;

namespace EduTrack.Application.Admin.Commands.CreateInviteCode;

/// <summary>Creates a single-use invite code that grants the given role.</summary>
public sealed record CreateInviteCodeCommand(
    long CallerTelegramUserId,
    UserRole Role,
    int? ExpiresInDays) : ICommand<InviteCodeDto>;
