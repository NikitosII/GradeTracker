using EduTrack.Domain.Notifications;

namespace EduTrack.Application.Notifications;

/// <summary>
/// A request to deliver one notification to one user. 
/// </summary>
public sealed record UserNotificationRequested(
    Guid NotificationId,
    Guid UserId,
    NotificationType Type,
    string Title,
    string Body,
    bool Important,
    Guid? ReminderId = null);
