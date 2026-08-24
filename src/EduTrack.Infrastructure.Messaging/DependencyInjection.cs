using EduTrack.Infrastructure.Messaging.Configuration;
using EduTrack.Infrastructure.Messaging.Consumers;
using EduTrack.Infrastructure.Messaging.Outbox;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EduTrack.Infrastructure.Messaging;

public static class DependencyInjection
{
    /// <summary>Notification queue name on RabbitMQ.</summary>
    public const string NotificationsQueue = "notifications.send";

    public static IServiceCollection AddMessagingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserNotificationConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                cfg.ReceiveEndpoint(NotificationsQueue, e =>
                {
                    // Transient send failures retry before the message is dead-lettered.
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                    e.ConfigureConsumer<UserNotificationConsumer>(context);
                });
            });
        });

        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
