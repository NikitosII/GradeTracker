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
    public static IServiceCollection AddTelegramInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient("telegram")
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
                return new TelegramBotClient(options.BotToken, httpClient);
            });

        services.AddScoped<ITelegramSender, TelegramSender>();
        services.AddHostedService<TelegramWebhookConfigurator>();

        return services;
    }
}
