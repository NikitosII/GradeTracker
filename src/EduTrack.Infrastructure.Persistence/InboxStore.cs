using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Application.Common.Time;
using EduTrack.Domain.Inbox;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EduTrack.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of the Telegram inbox idempotency guard
/// </summary>
public sealed class InboxStore : IInboxStore
{
    private const string UniqueViolation = "23505";

    private readonly EduTrackDbContext _db;
    private readonly IDateTimeProvider _clock;

    public InboxStore(EduTrackDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<bool> TryRegisterAsync(long updateId, CancellationToken cancellationToken = default)
    {
        // Fast path: skip an update we have already recorded. Also lets the
        // in-memory provider (used in tests) deduplicate without a DB constraint.
        if (await _db.InboxMessages.AnyAsync(m => m.UpdateId == updateId, cancellationToken))
        {
            return false;
        }

        var message = InboxMessage.Receive(updateId, _clock.UtcNow);
        _db.InboxMessages.Add(message);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Concurrent delivery of the same update raced past the pre-check;
            // the unique index rejected the duplicate. Skip it.
            _db.Entry(message).State = EntityState.Detached;
            return false;
        }
    }

    public async Task MarkProcessedAsync(long updateId, CancellationToken cancellationToken = default)
    {
        var message = await _db.InboxMessages.FirstOrDefaultAsync(m => m.UpdateId == updateId, cancellationToken);
        if (message is null)
        {
            return;
        }

        message.MarkProcessed(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(long updateId, string error, CancellationToken cancellationToken = default)
    {
        var message = await _db.InboxMessages.FirstOrDefaultAsync(m => m.UpdateId == updateId, cancellationToken);
        if (message is null)
        {
            return;
        }

        message.MarkFailed(error);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
