using System.Diagnostics.Metrics;
using EduTrack.Application.Abstractions.Observability;
using EduTrack.Domain.Notifications;

namespace EduTrack.Infrastructure.Observability;

/// <summary>
/// Records EduTrack's business and delivery metrics through a <see cref="Meter"/>, which
/// OpenTelemetry scrapes and exports. Instruments are created once and reused.
/// </summary>
public sealed class ApplicationMetrics : IApplicationMetrics, IDisposable
{
    private readonly Meter _meter;

    private readonly Counter<long> _accountsLinked;
    private readonly Counter<long> _gradesAdded;
    private readonly Counter<long> _gradesUpdated;
    private readonly Counter<long> _deadlinesCreated;
    private readonly Counter<long> _notificationsSent;
    private readonly Counter<long> _notificationsSuppressed;
    private readonly Counter<long> _notificationsFailed;
    private readonly Counter<long> _telegramRateLimited;
    private readonly Counter<long> _webhooksProcessed;

    // Observable gauge fed by the outbox processor; read on scrape.
    private long _outboxPending;

    public ApplicationMetrics()
    {
        _meter = new Meter(EduTrackTelemetry.MeterName);

        _accountsLinked = _meter.CreateCounter<long>(
            "edutrack.accounts.linked", unit: "{account}", description: "Telegram accounts linked to a user.");
        _gradesAdded = _meter.CreateCounter<long>(
            "edutrack.grades.added", unit: "{grade}", description: "Grades added by students.");
        _gradesUpdated = _meter.CreateCounter<long>(
            "edutrack.grades.updated", unit: "{grade}", description: "Grades edited by students.");
        _deadlinesCreated = _meter.CreateCounter<long>(
            "edutrack.deadlines.created", unit: "{deadline}", description: "Deadlines created by students.");
        _notificationsSent = _meter.CreateCounter<long>(
            "edutrack.notifications.sent", unit: "{notification}", description: "Notifications delivered to Telegram.");
        _notificationsSuppressed = _meter.CreateCounter<long>(
            "edutrack.notifications.suppressed", unit: "{notification}", description: "Notifications withheld by delivery policy.");
        _notificationsFailed = _meter.CreateCounter<long>(
            "edutrack.notifications.failed", unit: "{notification}", description: "Notifications that could not be delivered.");
        _telegramRateLimited = _meter.CreateCounter<long>(
            "edutrack.telegram.rate_limited", unit: "{response}", description: "Telegram API responses with HTTP 429.");
        _webhooksProcessed = _meter.CreateCounter<long>(
            "edutrack.webhook.processed", unit: "{update}", description: "Telegram webhook updates processed.");

        _meter.CreateObservableGauge(
            "edutrack.outbox.pending",
            () => Interlocked.Read(ref _outboxPending),
            unit: "{message}",
            description: "Unprocessed transactional-outbox messages.");
    }

    public void AccountLinked() => _accountsLinked.Add(1);

    public void GradeAdded() => _gradesAdded.Add(1);

    public void GradeUpdated() => _gradesUpdated.Add(1);

    public void DeadlineCreated() => _deadlinesCreated.Add(1);

    public void NotificationSent(NotificationType type) =>
        _notificationsSent.Add(1, new KeyValuePair<string, object?>("type", type.ToString()));

    public void NotificationSuppressed(NotificationType type) =>
        _notificationsSuppressed.Add(1, new KeyValuePair<string, object?>("type", type.ToString()));

    public void NotificationFailed(NotificationType type) =>
        _notificationsFailed.Add(1, new KeyValuePair<string, object?>("type", type.ToString()));

    public void TelegramRateLimited() => _telegramRateLimited.Add(1);

    public void WebhookProcessed(bool success) =>
        _webhooksProcessed.Add(1, new KeyValuePair<string, object?>("outcome", success ? "success" : "failure"));

    public void RecordOutboxPending(long count) => Interlocked.Exchange(ref _outboxPending, count);

    public void Dispose() => _meter.Dispose();
}
