using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Studies;

namespace EduTrack.Application.Studies.Commands.UpdateOwnAssignment;

public sealed record UpdateOwnAssignmentCommand(
    long TelegramUserId,
    Guid AssignmentId,
    AssignmentType Type,
    string Title,
    string? Description,
    DateTime DueAtUtc) : ICommand<AssignmentDto>;
