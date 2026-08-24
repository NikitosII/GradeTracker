using EduTrack.Application.Abstractions.Notifications;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Common.Time;
using EduTrack.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduTrack.Application.Notifications;

internal sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IApplicationDbContext _db;
    private readonly ITelegramSender _telegram;
    private readonly IDateTimeProvider _clock;
    private readonly NotificationOptions _options;

    public NotificationDispatcher(
        IApplicationDbContext db,
        ITelegramSender telegram,
        IDateTimeProvider clock,
        IOptions<NotificationOptions> options)
    {
        _db = db;
        _telegram = telegram;
        _clock = clock;
        _options = options.Value;
    }

    public async Task DispatchAsync(UserNotificationRequested message, CancellationToken cancellationToken)
    {
        // Idempotency: a log row already exists for this notification id.
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
            return;
        }

        if (!message.Important)
        {
            if (!user.IsNotificationsEnabled)
            {
                await SuppressAsync(message, "Notifications disabled.", now, cancellationToken);
                return;
            }

            if (QuietHours.IsWithin(now, user.TimeZone, _options.QuietHoursStart, _options.QuietHoursEnd))
            {
                await SuppressAsync(message, "Quiet hours.", now, cancellationToken);
                return;
            }
        }

        // A send failure throws here so the broker can retry; nothing is persisted yet.
        var text = string.IsNullOrWhiteSpace(message.Title)
            ? message.Body
            : $"{message.Title}\n\n{message.Body}";
        var telegramMessageId = await _telegram.SendNotificationAsync(user.TelegramUserId, text, cancellationToken);

        _db.NotificationLogs.Add(NotificationLog.Sent(
            message.NotificationId, message.UserId, message.Type, telegramMessageId, now));
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SuppressAsync(UserNotificationRequested message, string reason, DateTime now, CancellationToken cancellationToken)
    {
        _db.NotificationLogs.Add(NotificationLog.Suppressed(
            message.NotificationId, message.UserId, message.Type, reason, now));
        await _db.SaveChangesAsync(cancellationToken);
    }
}
