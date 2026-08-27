namespace EduTrack.Infrastructure.Scheduling;

/// <summary>Scheduler tuning for the background jobs.</summary>
public sealed class SchedulingOptions
{
    public const string SectionName = "Scheduling";

    /// <summary>How often the due-reminder scan runs, in seconds.</summary>
    public int DueRemindersIntervalSeconds { get; set; } = 60;

    /// <summary>Maximum reminders handed to the pipeline per scan.</summary>
    public int DueRemindersBatchSize { get; set; } = 100;

    /// <summary>Cron for the morning-digest check. Quartz cron format.</summary>
    public string MorningDigestCron { get; set; } = "0 0 * * * ?";
}
