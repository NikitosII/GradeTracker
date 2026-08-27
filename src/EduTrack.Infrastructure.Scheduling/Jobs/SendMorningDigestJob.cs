using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Notifications;
using EduTrack.Application.Reminders.Digests;
using EduTrack.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace EduTrack.Infrastructure.Scheduling.Jobs;

/// <summary>
/// Runs hourly and sends the morning digest to each user whose local time has just reached the configured digest hour. 
/// </summary>
[DisallowConcurrentExecution]
public sealed class SendMorningDigestJob : IJob
{
    private readonly IApplicationDbContext _db;
    private readonly IMorningDigestComposer _composer;
    private readonly IDateTimeProvider _clock;
    private readonly NotificationOptions _notifications;
    private readonly ILogger<SendMorningDigestJob> _logger;

    public SendMorningDigestJob(
        IApplicationDbContext db,
        IMorningDigestComposer composer,
        IDateTimeProvider clock,
        IOptions<NotificationOptions> notifications,
        ILogger<SendMorningDigestJob> logger)
    {
        _db = db;
        _composer = composer;
        _clock = clock;
        _notifications = notifications.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = _clock.UtcNow;

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsNotificationsEnabled)
            .ToListAsync(context.CancellationToken);

        var sent = 0;

        foreach (var user in users)
        {
            var tz = ResolveTimeZone(user.TimeZone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), tz);

            if (local.Hour != _notifications.MorningDigestHour)
            {
                continue;
            }

            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified), tz);

            // Guard against a second send within the same local day.
            var alreadySent = await _db.NotificationLogs
                .AsNoTracking()
                .AnyAsync(
                    l => l.UserId == user.Id
                        && l.Type == NotificationType.MorningDigest
                        && l.CreatedAtUtc >= dayStartUtc,
                    context.CancellationToken);

            if (alreadySent)
            {
                continue;
            }

            var body = await _composer.ComposeAsync(user, now, context.CancellationToken);
            if (body is null)
            {
                continue;
            }

            OutboxWriter.Enqueue(_db, new UserNotificationRequested(
                Guid.NewGuid(),
                user.Id,
                NotificationType.MorningDigest,
                "Good morning!",
                body,
                Important: false), now);

            sent++;
        }

        if (sent > 0)
        {
            await _db.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Enqueued {Count} morning digest(s).", sent);
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
