namespace EduTrack.Application.Studies;

public sealed record GradeDto(
    Guid Id,
    Guid SubjectId,
    string SubjectName,
    int Value,
    decimal Weight,
    string? Comment,
    DateTime OccurredAt);
