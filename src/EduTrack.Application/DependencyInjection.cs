using EduTrack.Application.Abstractions.Notifications;
using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Common.Behaviors;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Notifications;
using EduTrack.Application.Reminders.Digests;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(MetricsBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.TryAddSingleton<IApplicationMetrics>(NullApplicationMetrics.Instance);
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddOptions<NotificationOptions>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IMorningDigestComposer, MorningDigestComposer>();

        return services;
    }
}
