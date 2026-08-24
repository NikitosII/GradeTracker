namespace EduTrack.Infrastructure.Messaging.Configuration;

/// <summary>RabbitMQ connection settings.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    /// <summary>How often the outbox is polled for pending messages.</summary>
    public int OutboxPollSeconds { get; set; } = 5;

    /// <summary>Maximum messages drained from the outbox per poll.</summary>
    public int OutboxBatchSize { get; set; } = 50;
}
