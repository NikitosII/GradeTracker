using EduTrack.Domain.Notifications;

namespace EduTrack.Application.Abstractions.Observability;

/// <summary>
/// Records business and delivery metrics.
/// </summary>
public interface IApplicationMetrics
{
    void AccountLinked();
    void GradeAdded();
    void GradeUpdated();
    void DeadlineCreated();
    void NotificationSent(NotificationType type);
    void NotificationSuppressed(NotificationType type);
    void NotificationFailed(NotificationType type);
    void TelegramRateLimited();

    void WebhookProcessed(bool success);
    void RecordOutboxPending(long count);
}
