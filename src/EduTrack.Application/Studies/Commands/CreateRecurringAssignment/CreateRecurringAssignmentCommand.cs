using EduTrack.Application.Common.Messaging;
using EduTrack.Domain.Studies;

namespace EduTrack.Application.Studies.Commands.CreateRecurringAssignment;

/// <summary>Creates a series of repeating deadlines, materialised as concrete assignments.</summary>
public sealed record CreateRecurringAssignmentCommand(
    long TelegramUserId,
    Guid SubjectId,
    AssignmentType Type,
    string Title,
    string? Description,
    DateTime FirstDueAtUtc,
    RecurrenceFrequency Frequency,
    int Interval,
    int Count) : ICommand<int>;
