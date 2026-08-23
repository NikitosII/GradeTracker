namespace EduTrack.Application.Admin;

public sealed record AuditLogEntryDto(
    Guid Id,
    string? ActorName,
    string Action,
    string EntityType,
    string? EntityId,
    DateTime CreatedAt);
