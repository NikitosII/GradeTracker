namespace EduTrack.Application.Admin;

public sealed record InviteCodeDto(
    Guid Id,
    string Code,
    string Role,
    DateTime? ExpiresAt,
    bool IsUsed,
    DateTime CreatedAt);
