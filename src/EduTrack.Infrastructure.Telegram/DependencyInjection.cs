using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Infrastructure.Telegram.Configuration;
using EduTrack.Infrastructure.Telegram.Webhook;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace EduTrack.Infrastructure.Telegram;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Telegram client and sender.
    /// </summary>
    public static IServiceCollection AddTelegramSender(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient("telegram")
            .RemoveAllLoggers()
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
                return new TelegramBotClient(options.BotToken, httpClient);
            });

        services.AddScoped<ITelegramSender, TelegramSender>();

        return services;
    }

    /// <summary>Registers the hosted service that publishes the webhook URL to Telegram on startup.</summary>
    public static IServiceCollection AddTelegramWebhook(this IServiceCollection services)
    {
        services.AddHostedService<TelegramWebhookConfigurator>();
        return services;
    }

    /// <summary>Web entry point: sender + webhook registration.</summary>
    public static IServiceCollection AddTelegramInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTelegramSender(configuration);
        services.AddTelegramWebhook();
        return services;
    }
}
