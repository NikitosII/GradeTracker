using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Time;
using EduTrack.Application.Notifications;
using EduTrack.Infrastructure.Messaging.Configuration;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduTrack.Infrastructure.Messaging.Outbox;

/// <summary>
/// Polls the outbox table and publishes pending messages to the broker, marking each
/// processed once handed off. 
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.OutboxPollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processing iteration failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var metrics = scope.ServiceProvider.GetRequiredService<IApplicationMetrics>();

        var pendingCount = await db.OutboxMessages
            .CountAsync(m => m.ProcessedAtUtc == null, cancellationToken);
        metrics.RecordOutboxPending(pendingCount);

        if (pendingCount == 0)
        {
            return;
        }

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(Math.Max(1, _options.OutboxBatchSize))
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                if (message.Type == OutboxWriter.UserNotificationType)
                {
                    var contract = OutboxWriter.Deserialize(message.Payload);
                    if (contract is not null)
                    {
                        await publish.Publish(contract, cancellationToken);
                    }
                }
                else
                {
                    _logger.LogWarning("Unknown outbox message type {Type}; skipping.", message.Type);
                }

                message.MarkProcessed(clock.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to publish outbox message {MessageId}.", message.Id);
                message.MarkFailed(ex.Message);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
