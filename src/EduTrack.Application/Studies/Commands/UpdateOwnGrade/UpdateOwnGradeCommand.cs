using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Studies.Commands.UpdateOwnGrade;

public sealed record UpdateOwnGradeCommand(
    long TelegramUserId,
    Guid GradeId,
    int Value,
    decimal Weight,
    string? Comment,
    DateTime OccurredAt) : ICommand<GradeDto>;
