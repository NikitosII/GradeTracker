using EduTrack.Domain.Users;

namespace EduTrack.Application.Reminders.Digests;

/// <summary>Builds the morning digest body for a user.</summary>
public interface IMorningDigestComposer
{
    Task<string?> ComposeAsync(User user, DateTime nowUtc, CancellationToken cancellationToken);
}
