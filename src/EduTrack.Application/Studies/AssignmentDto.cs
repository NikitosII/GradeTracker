using EduTrack.Domain.Studies;

namespace EduTrack.Application.Studies;

public sealed record AssignmentDto(
    Guid Id,
    Guid SubjectId,
    string SubjectName,
    AssignmentType Type,
    string Title,
    string? Description,
    DateTime DueAtUtc);
