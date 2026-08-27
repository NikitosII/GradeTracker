using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Notifications;
using EduTrack.Application.Users;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduTrack.Application.Reminders.Commands.SnoozeReminder;

internal sealed class SnoozeReminderCommandHandler : ICommandHandler<SnoozeReminderCommand, DateTime>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly NotificationOptions _options;

    public SnoozeReminderCommandHandler(
        IApplicationDbContext db,
        IDateTimeProvider clock,
        IOptions<NotificationOptions> options)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<Result<DateTime>> Handle(SnoozeReminderCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<DateTime>(UserErrors.NotBound);
        }

        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == request.ReminderId, cancellationToken);

        if (reminder is null)
        {
            return Result.Failure<DateTime>(ReminderErrors.NotFound);
        }

        if (reminder.UserId != user.Id)
        {
            return Result.Failure<DateTime>(ReminderErrors.NotOwner);
        }

        var now = _clock.UtcNow;
        var sendAtUtc = ResolveSendAt(request.Option, now, user.TimeZone);

        reminder.Reschedule(sendAtUtc, now);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(sendAtUtc);
    }

    private DateTime ResolveSendAt(SnoozeOption option, DateTime nowUtc, string? timeZoneId) => option switch
    {
        SnoozeOption.OneHour => nowUtc.AddHours(1),
        SnoozeOption.ThreeHours => nowUtc.AddHours(3),
        SnoozeOption.TomorrowMorning => TomorrowMorningUtc(nowUtc, timeZoneId),
        _ => nowUtc.AddHours(1),
    };

    private DateTime TomorrowMorningUtc(DateTime nowUtc, string? timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);
        var target = local.Date.AddDays(1).AddHours(_options.MorningDigestHour);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(target, DateTimeKind.Unspecified), tz);
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
