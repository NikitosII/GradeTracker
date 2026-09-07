using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Localization;
using EduTrack.Application.Notifications;
using EduTrack.Application.Reminders;
using EduTrack.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace EduTrack.Infrastructure.Scheduling.Jobs;

/// <summary>
/// Scans for reminders whose time has come and hands each to the notification pipeline
/// </summary>
[DisallowConcurrentExecution]
public sealed class SendDueRemindersJob : IJob
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly SchedulingOptions _options;
    private readonly ITranslator _translator;
    private readonly ILogger<SendDueRemindersJob> _logger;

    public SendDueRemindersJob(
        IApplicationDbContext db,
        IDateTimeProvider clock,
        IOptions<SchedulingOptions> options,
        ITranslator translator,
        ILogger<SendDueRemindersJob> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _translator = translator;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = _clock.UtcNow;
        var batchSize = Math.Max(1, _options.DueRemindersBatchSize);

        var due = await (
            from r in _db.Reminders
            where r.Status == ReminderStatus.Pending && r.SendAtUtc <= now
            join a in _db.Assignments on r.AssignmentId equals a.Id
            join s in _db.Subjects on a.SubjectId equals s.Id
            join u in _db.Users on r.UserId equals u.Id
            orderby r.SendAtUtc
            select new DueReminder(r, s.Name, a.Title, a.DueAtUtc, u.TimeZone, u.Language))
            .Take(batchSize)
            .ToListAsync(context.CancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var item in due)
        {
            var (type, title, body) = ReminderNotification.Build(
                _translator, item.Reminder.Kind, item.SubjectName, item.AssignmentTitle, item.DueAtUtc, item.TimeZone, item.Language);

            OutboxWriter.Enqueue(_db, new UserNotificationRequested(
                Guid.NewGuid(),
                item.Reminder.UserId,
                type,
                title,
                body,
                Important: false,
                ReminderId: item.Reminder.Id), now);

            item.Reminder.MarkSent(now);
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Enqueued {Count} due reminder(s).", due.Count);
    }

    private sealed record DueReminder(
        Reminder Reminder,
        string SubjectName,
        string AssignmentTitle,
        DateTime DueAtUtc,
        string? TimeZone,
        string? Language);
}
