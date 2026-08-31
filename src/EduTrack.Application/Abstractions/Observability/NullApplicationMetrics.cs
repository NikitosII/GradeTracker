using EduTrack.Domain.Notifications;

namespace EduTrack.Application.Abstractions.Observability;

public sealed class NullApplicationMetrics : IApplicationMetrics
{
    public static readonly NullApplicationMetrics Instance = new();

    private NullApplicationMetrics()
    {
    }

    public void AccountLinked()
    {
    }

    public void GradeAdded()
    {
    }

    public void GradeUpdated()
    {
    }

    public void DeadlineCreated()
    {
    }

    public void NotificationSent(NotificationType type)
    {
    }

    public void NotificationSuppressed(NotificationType type)
    {
    }

    public void NotificationFailed(NotificationType type)
    {
    }

    public void TelegramRateLimited()
    {
    }

    public void WebhookProcessed(bool success)
    {
    }

    public void RecordOutboxPending(long count)
    {
    }
}
