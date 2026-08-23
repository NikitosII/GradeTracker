using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies;

namespace EduTrack.Application.Admin.Commands.UpdateSubject;

/// <summary>Renames a subject and/or toggles its active state.</summary>
public sealed record UpdateSubjectCommand(
    long CallerTelegramUserId,
    Guid SubjectId,
    string Name,
    string? Description,
    bool IsActive) : ICommand<SubjectDto>;
