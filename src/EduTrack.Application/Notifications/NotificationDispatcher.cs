using EduTrack.Application.Abstractions.Notifications;
using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Reminders;
using EduTrack.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduTrack.Application.Notifications;

internal sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IApplicationDbContext _db;
    private readonly ITelegramSender _telegram;
    private readonly IDateTimeProvider _clock;
    private readonly IApplicationMetrics _metrics;
    private readonly NotificationOptions _options;

    public NotificationDispatcher(
        IApplicationDbContext db,
        ITelegramSender telegram,
        IDateTimeProvider clock,
        IApplicationMetrics metrics,
        IOptions<NotificationOptions> options)
    {
        _db = db;
        _telegram = telegram;
        _clock = clock;
        _metrics = metrics;
        _options = options.Value;
    }

    public async Task DispatchAsync(UserNotificationRequested message, CancellationToken cancellationToken)
    {
        var alreadyHandled = await _db.NotificationLogs
            .AsNoTracking()
            .AnyAsync(l => l.Id == message.NotificationId, cancellationToken);

        if (alreadyHandled)
        {
            return;
        }

        var now = _clock.UtcNow;

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == message.UserId, cancellationToken);

        if (user is null)
        {
            _db.NotificationLogs.Add(NotificationLog.Failed(
                message.NotificationId, message.UserId, message.Type, "Recipient not found.", now));
            await _db.SaveChangesAsync(cancellationToken);
            _metrics.NotificationFailed(message.Type);
            return;
        }

        if (!message.Important)
        {
            if (!user.IsNotificationsEnabled)
            {
                await SuppressAsync(message, "Notifications disabled.", now, cancellationToken);
                return;
            }

            if (IsMutedByPreference(user, message.Type))
            {
                await SuppressAsync(message, "Muted by user settings.", now, cancellationToken);
                return;
            }

            var quietStart = user.QuietHoursStart ?? _options.QuietHoursStart;
            var quietEnd = user.QuietHoursEnd ?? _options.QuietHoursEnd;
            if (QuietHours.IsWithin(now, user.TimeZone, quietStart, quietEnd))
            {
                await SuppressAsync(message, "Quiet hours.", now, cancellationToken);
                return;
            }
        }

        var text = string.IsNullOrWhiteSpace(message.Title) ? message.Body : $"{message.Title}\n\n{message.Body}";
        var buttons = BuildSnoozeButtons(message);

        int telegramMessageId;
        try
        {
            telegramMessageId = await _telegram.SendNotificationAsync(user.TelegramUserId, text, buttons, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.NotificationFailed(message.Type);
            throw;
        }

        _db.NotificationLogs.Add(NotificationLog.Sent(
            message.NotificationId, message.UserId, message.Type, telegramMessageId, now));
        await _db.SaveChangesAsync(cancellationToken);
        _metrics.NotificationSent(message.Type);
    }

    /// <summary>Whether the user has switched off this specific kind of non-critical notification.</summary>
    private static bool IsMutedByPreference(Domain.Users.User user, NotificationType type) => type switch
    {
        NotificationType.AssignmentReminder24h => !user.Reminder24hEnabled,
        NotificationType.AssignmentReminder2h => !user.Reminder2hEnabled,
        NotificationType.MorningDigest => !user.MorningDigestEnabled,
        _ => false,
    };

    /// <summary>Reminder notifications carry snooze buttons keyed by the reminder id.</summary>
    private static IReadOnlyList<IReadOnlyList<InlineButton>>? BuildSnoozeButtons(UserNotificationRequested message)
    {
        if (message.ReminderId is not { } reminderId)
        {
            return null;
        }

        var isReminder = message.Type is NotificationType.AssignmentReminder24h
            or NotificationType.AssignmentReminder2h
            or NotificationType.AssignmentOverdue;

        if (!isReminder)
        {
            return null;
        }

        return new[]
        {
            new[]
            {
                new InlineButton("1 hour", ReminderCallback.Snooze(reminderId, SnoozeOption.OneHour)),
                new InlineButton("3 hours", ReminderCallback.Snooze(reminderId, SnoozeOption.ThreeHours)),
                new InlineButton("Tomorrow", ReminderCallback.Snooze(reminderId, SnoozeOption.TomorrowMorning)),
            },
        };
    }

    private async Task SuppressAsync(UserNotificationRequested message, string reason, DateTime now, CancellationToken cancellationToken)
    {
        _db.NotificationLogs.Add(NotificationLog.Suppressed(
            message.NotificationId, message.UserId, message.Type, reason, now));
        await _db.SaveChangesAsync(cancellationToken);
        _metrics.NotificationSuppressed(message.Type);
    }
}
