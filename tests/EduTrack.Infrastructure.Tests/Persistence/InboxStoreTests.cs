using EduTrack.Application.Common.Time;
using EduTrack.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EduTrack.Infrastructure.Tests.Persistence;

/// <summary>
/// Verifies the Telegram inbox idempotency guard against a real Postgres engine,
/// including the concurrent-delivery race that only the unique index can resolve.
/// </summary>
public sealed class InboxStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    private readonly IDateTimeProvider _clock = new FixedClock(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task First_registration_succeeds_and_redelivery_is_skipped()
    {
        var store = new InboxStore(CreateContext(), _clock);

        (await store.TryRegisterAsync(1001)).Should().BeTrue();

        // A separate store/context simulates the redelivery arriving later.
        var again = new InboxStore(CreateContext(), _clock);
        (await again.TryRegisterAsync(1001)).Should().BeFalse();
    }

    [Fact]
    public async Task Concurrent_registrations_admit_exactly_one()
    {
        const long updateId = 2002;

        // Each task uses its own context, racing on the same update_id. The unique
        // index guarantees only one insert wins; the loser catches 23505.
        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ =>
                Task.Run(() => new InboxStore(CreateContext(), _clock).TryRegisterAsync(updateId))));

        results.Count(registered => registered).Should().Be(1);

        await using var db = CreateContext();
        (await db.InboxMessages.CountAsync(m => m.UpdateId == updateId)).Should().Be(1);
    }

    [Fact]
    public async Task MarkProcessed_stamps_the_message()
    {
        var store = new InboxStore(CreateContext(), _clock);
        await store.TryRegisterAsync(3003);

        await store.MarkProcessedAsync(3003);

        await using var db = CreateContext();
        var message = await db.InboxMessages.SingleAsync(m => m.UpdateId == 3003);
        message.ProcessedAtUtc.Should().Be(_clock.UtcNow);
        message.Error.Should().BeNull();
    }

    [Fact]
    public async Task MarkFailed_records_the_error()
    {
        var store = new InboxStore(CreateContext(), _clock);
        await store.TryRegisterAsync(4004);

        await store.MarkFailedAsync(4004, "boom");

        await using var db = CreateContext();
        var message = await db.InboxMessages.SingleAsync(m => m.UpdateId == 4004);
        message.Error.Should().Be("boom");
        message.ProcessedAtUtc.Should().BeNull();
    }

    private EduTrackDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EduTrackDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql =>
                npgsql.MigrationsAssembly(typeof(EduTrackDbContext).Assembly.FullName))
            .Options;

        return new EduTrackDbContext(options);
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
