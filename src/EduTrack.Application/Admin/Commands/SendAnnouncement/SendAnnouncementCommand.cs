using EduTrack.Application.Common.Messaging;

namespace EduTrack.Application.Admin.Commands.SendAnnouncement;

/// <summary>Broadcasts a system announcement to every user.</summary>
public sealed record SendAnnouncementCommand(
    long CallerTelegramUserId,
    string Text) : ICommand<int>;
