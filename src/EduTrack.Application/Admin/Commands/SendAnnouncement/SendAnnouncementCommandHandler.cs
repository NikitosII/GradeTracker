using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Messaging;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Localization;
using EduTrack.Application.Notifications;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Common;
using EduTrack.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Admin.Commands.SendAnnouncement;

internal sealed class SendAnnouncementCommandHandler : ICommandHandler<SendAnnouncementCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ITranslator _translator;

    public SendAnnouncementCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ITranslator translator)
    {
        _db = db;
        _clock = clock;
        _translator = translator;
    }

    public async Task<Result<int>> Handle(SendAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var gate = await AdminGuard.RequireAdminAsync(_db, request.CallerTelegramUserId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<int>(gate.Error);
        }

        var now = _clock.UtcNow;
        var text = request.Text.Trim();

        var recipients = await _db.Users
            .AsNoTracking()
            .Select(u => new { u.Id, u.Language })
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            var title = _translator.Find(recipient.Language, TextKeys.NotifyAnnouncementTitle) ?? "Announcement";

            OutboxWriter.Enqueue(_db, new UserNotificationRequested(
                Guid.NewGuid(),
                recipient.Id,
                NotificationType.SystemAnnouncement,
                title,
                text,
                Important: false), now);
        }

        _db.AuditLogs.Add(AuditLog.Create(
            gate.Value.Id,
            AuditActions.AnnouncementSent,
            AuditEntities.Announcement,
            null,
            oldValue: null,
            newValue: $"{recipients.Count} recipients",
            now));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(recipients.Count);
    }
}
