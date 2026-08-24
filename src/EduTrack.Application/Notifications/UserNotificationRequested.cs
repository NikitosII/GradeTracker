using EduTrack.Domain.Notifications;

namespace EduTrack.Application.Notifications;

/// <summary>
/// A request to deliver one notification to one user. Written to the outbox by a command
/// handler, published to RabbitMQ, and consumed by the notification pipeline.
/// </summary>
public sealed record UserNotificationRequested(
    Guid NotificationId,
    Guid UserId,
    NotificationType Type,
    string Title,
    string Body,
    bool Important);
