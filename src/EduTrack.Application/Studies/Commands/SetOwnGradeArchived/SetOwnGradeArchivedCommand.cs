using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Studies.Commands.SetOwnGradeArchived;

/// <summary>Archives or restores one of the caller's own grades.</summary>
public sealed record SetOwnGradeArchivedCommand(long TelegramUserId, Guid GradeId, bool Archived) : ICommand;
