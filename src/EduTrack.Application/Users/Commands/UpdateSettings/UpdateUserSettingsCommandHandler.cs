using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Admin;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Users.Commands.UpdateSettings;

internal sealed class UpdateUserSettingsCommandHandler : ICommandHandler<UpdateUserSettingsCommand, UserSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateUserSettingsCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result<UserSettingsDto>> Handle(UpdateUserSettingsCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramUserId == request.TelegramUserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserSettingsDto>(UserErrors.NotBound);
        }

        var before = user.ToSettingsDto();
        var now = _clock.UtcNow;

        user.SetTimeZone(request.TimeZone, now);
        user.SetLanguage(request.Language, now);
        user.SetNotificationsEnabled(request.NotificationsEnabled, now);
        user.SetMorningDigestEnabled(request.MorningDigestEnabled, now);
        user.SetReminder24hEnabled(request.Reminder24hEnabled, now);
        user.SetReminder2hEnabled(request.Reminder2hEnabled, now);
        user.SetQuietHours(request.QuietHoursStart, request.QuietHoursEnd, now);

        var after = user.ToSettingsDto();

        if (before != after)
        {
            _db.AuditLogs.Add(AuditLog.Create(
                user.Id,
                AuditActions.SettingsUpdated,
                AuditEntities.User,
                user.Id.ToString(),
                oldValue: Summarize(before),
                newValue: Summarize(after),
                now));

            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(after);
    }

    private static string Summarize(UserSettingsDto s)
    {
        var quiet = s.QuietHoursStart is { } start && s.QuietHoursEnd is { } end ? $"{start}-{end}" : "off";
        return $"tz={s.TimeZone};lang={s.Language};notif={s.NotificationsEnabled};digest={s.MorningDigestEnabled};" +
            $"r24={s.Reminder24hEnabled};r2={s.Reminder2hEnabled};quiet={quiet}";
    }
}
