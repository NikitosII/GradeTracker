using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Studies;

namespace EduTrack.Application.Studies.Commands.CreateOwnAssignment;

public sealed record CreateOwnAssignmentCommand(
    long TelegramUserId,
    Guid SubjectId,
    AssignmentType Type,
    string Title,
    string? Description,
    DateTime DueAtUtc) : ICommand<AssignmentDto>;
