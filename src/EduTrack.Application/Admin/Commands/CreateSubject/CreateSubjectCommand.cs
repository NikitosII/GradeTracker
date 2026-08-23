using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Studies;

namespace EduTrack.Application.Admin.Commands.CreateSubject;

/// <summary>Adds a new subject to the reference book.</summary>
public sealed record CreateSubjectCommand(
    long CallerTelegramUserId,
    string Name,
    string? Description) : ICommand<SubjectDto>;
