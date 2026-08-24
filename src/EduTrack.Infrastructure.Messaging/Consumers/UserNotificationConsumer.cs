using EduTrack.Application.Abstractions.Notifications;
using EduTrack.Application.Notifications;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EduTrack.Infrastructure.Messaging.Consumers;

/// <summary>
/// Receives notification requests from RabbitMQ and hands them to the dispatcher, which
/// applies delivery policy and sends. 
/// </summary>
public sealed class UserNotificationConsumer : IConsumer<UserNotificationRequested>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<UserNotificationConsumer> _logger;

    public UserNotificationConsumer(INotificationDispatcher dispatcher, ILogger<UserNotificationConsumer> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserNotificationRequested> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Dispatching {Type} notification {NotificationId} to user {UserId}",
            message.Type, message.NotificationId, message.UserId);

        await _dispatcher.DispatchAsync(message, context.CancellationToken);
    }
}
