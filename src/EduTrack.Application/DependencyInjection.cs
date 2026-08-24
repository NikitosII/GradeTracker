using EduTrack.Application.Abstractions.Notifications;
using EduTrack.Application.Common.Behaviors;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Notifications;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

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
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Default options; a host (e.g. the worker) may bind these from configuration.
        services.AddOptions<NotificationOptions>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        return services;
    }
}
