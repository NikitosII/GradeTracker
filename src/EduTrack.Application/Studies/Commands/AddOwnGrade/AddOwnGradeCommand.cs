using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Studies.Commands.AddOwnGrade;

public sealed record AddOwnGradeCommand(
    long TelegramUserId,
    Guid SubjectId,
    int Value,
    decimal Weight,
    string? Comment,
    DateTime OccurredAt) : ICommand<GradeDto>;
