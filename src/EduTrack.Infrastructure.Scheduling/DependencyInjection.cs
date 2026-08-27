using EduTrack.Infrastructure.Scheduling.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace EduTrack.Infrastructure.Scheduling;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SchedulingOptions>()
            .Bind(configuration.GetSection(SchedulingOptions.SectionName));

        var options = configuration.GetSection(SchedulingOptions.SectionName).Get<SchedulingOptions>()
            ?? new SchedulingOptions();

        services.AddQuartz(q =>
        {
            var remindersKey = new JobKey("send-due-reminders");
            q.AddJob<SendDueRemindersJob>(remindersKey);
            q.AddTrigger(trigger => trigger
                .ForJob(remindersKey)
                .StartNow()
                .WithSimpleSchedule(schedule => schedule
                    .WithInterval(TimeSpan.FromSeconds(Math.Max(5, options.DueRemindersIntervalSeconds)))
                    .RepeatForever()));

            var digestKey = new JobKey("send-morning-digest");
            q.AddJob<SendMorningDigestJob>(digestKey);
            q.AddTrigger(trigger => trigger
                .ForJob(digestKey)
                .WithCronSchedule(options.MorningDigestCron));
        });

        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }
}
