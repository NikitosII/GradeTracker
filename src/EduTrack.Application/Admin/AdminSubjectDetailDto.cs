namespace EduTrack.Application.Admin;

public sealed record AdminSubjectDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive);
