using EduTrack.Infrastructure.Telegram.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace EduTrack.Infrastructure.Telegram.Webhook;

/// <summary>
/// Registers the bot's webhook URL with Telegram on application startup.
/// </summary>
public sealed class TelegramWebhookConfigurator : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramWebhookConfigurator> _logger;

    public TelegramWebhookConfigurator(ITelegramBotClient botClient, IOptions<TelegramOptions> options, ILogger<TelegramWebhookConfigurator> logger)
    {
        _botClient = botClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogInformation("Telegram webhook URL not configured; skipping webhook registration.");
            return;
        }

        await _botClient.SetWebhook(
            url: _options.WebhookUrl,
            secretToken: _options.WebhookSecret,
            dropPendingUpdates: true,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Telegram webhook registered at {WebhookUrl}", _options.WebhookUrl);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
