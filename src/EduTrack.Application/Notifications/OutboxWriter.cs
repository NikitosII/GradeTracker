using System.Text.Json;
using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Domain.Outbox;

namespace EduTrack.Application.Notifications;

/// <summary>
/// Serializes notification requests into the outbox.
/// </summary>
public static class OutboxWriter
{
    public const string UserNotificationType = nameof(UserNotificationRequested);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static void Enqueue(IApplicationDbContext db, UserNotificationRequested message, DateTime nowUtc)
    {
        var payload = JsonSerializer.Serialize(message, SerializerOptions);
        db.OutboxMessages.Add(OutboxMessage.Create(UserNotificationType, payload, nowUtc));
    }

    public static UserNotificationRequested? Deserialize(string payload)
        => JsonSerializer.Deserialize<UserNotificationRequested>(payload, SerializerOptions);
}
