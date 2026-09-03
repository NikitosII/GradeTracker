using EduTrack.Domain.Outbox;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using EduTrack.Infrastructure.Persistence;
using EduTrack.Integration.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduTrack.Integration.Tests;

/// <summary>
/// Verifies that the EF Core model works against a real PostgreSQL engine, not
/// just the in-memory provider used by the unit tests: migrations apply, the full
/// schema is reachable, decimal/date columns round-trip, and the outbox table
/// behaves as the poller expects.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DatabaseIntegrationTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly EduTrackWebAppFactory _factory;

    public DatabaseIntegrationTests(PostgresFixture postgres)
    {
        _factory = new EduTrackWebAppFactory(postgres.ConnectionString);
        // Building a client forces the host to start, which runs DatabaseInitializer
        // (i.e. applies all migrations) against the container.
        _factory.CreateClient();
    }

    [Fact]
    public async Task Migrations_are_applied_and_the_schema_is_reachable()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTrackDbContext>();

        (await db.Database.CanConnectAsync()).Should().BeTrue();
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();

        // Every core table is queryable, proving the migration built the full schema.
        var act = async () =>
        {
            await db.Users.CountAsync();
            await db.Grades.CountAsync();
            await db.Assignments.CountAsync();
            await db.Reminders.CountAsync();
            await db.OutboxMessages.CountAsync();
        };
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Grade_round_trips_weight_and_date_through_postgres()
    {
        var gradeId = await _factory.WithScopeAsync<Guid>(async db =>
        {
            var user = User.Register(9100, "deci", "Deci", null, UserRole.Student, Now);
            var subject = Subject.Create("Precision", null, Now);
            var grade = Grade.Add(user.Id, subject.Id, 4, 2.5m, "weighted", Now, user.Id, Now);
            db.Users.Add(user);
            db.Subjects.Add(subject);
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            return grade.Id;
        });

        await _factory.WithDbContextAsync(async db =>
        {
            var stored = await db.Grades.AsNoTracking().SingleAsync(g => g.Id == gradeId);
            stored.Weight.Should().Be(2.5m);
            stored.Value.Should().Be(4);
            stored.OccurredAt.Should().Be(Now);
        });
    }

    [Fact]
    public async Task Outbox_message_is_stored_pending_and_can_be_marked_processed()
    {
        var id = await _factory.WithScopeAsync<Guid>(async db =>
        {
            var message = OutboxMessage.Create("IntegrationTest", "{\"hello\":\"world\"}", Now);
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
            return message.Id;
        });

        await _factory.WithDbContextAsync(async db =>
        {
            var pending = await db.OutboxMessages.SingleAsync(m => m.Id == id);
            pending.ProcessedAtUtc.Should().BeNull();
            pending.Payload.Should().Contain("world");

            pending.MarkProcessed(Now.AddMinutes(1));
            await db.SaveChangesAsync();
        });

        await _factory.WithDbContextAsync(async db =>
        {
            var processed = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id);
            processed.ProcessedAtUtc.Should().NotBeNull();
        });
    }

    public void Dispose() => _factory.Dispose();
}
