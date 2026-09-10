using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Studies.Commands.SetOwnAssignmentArchived;

/// <summary>Archives or restores one of the caller's own deadlines.</summary>
public sealed record SetOwnAssignmentArchivedCommand(long TelegramUserId, Guid AssignmentId, bool Archived) : ICommand;
