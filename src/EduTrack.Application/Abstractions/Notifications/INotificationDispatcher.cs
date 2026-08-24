using EduTrack.Application.Notifications;

namespace EduTrack.Application.Abstractions.Notifications;

/// <summary>
/// Applies delivery policy (idempotency, user settings, quiet hours) and sends a notification. 
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(UserNotificationRequested message, CancellationToken cancellationToken);
}
