using EduTrack.Domain.Users;
using EduTrack.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace EduTrack.Infrastructure.Tests.Persistence;

/// <summary>
/// Verifies the startup initializer against a real Postgres engine: it applies
/// the migrations from scratch and seeds the bootstrap admin invite code exactly
/// once. Subjects are not seeded — an admin creates them through the bot.
/// </summary>
public sealed class DatabaseInitializerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Applies_migrations_and_seeds_admin_code_without_subjects()
    {
        const string adminCode = "INIT-ADMIN-CODE";
        await using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [DatabaseInitializer.BootstrapAdminCodeKey] = adminCode,
        });

        var initializer = ActivatorUtilities.CreateInstance<DatabaseInitializer>(provider);
        await initializer.StartAsync(CancellationToken.None);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTrackDbContext>();

        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();

        var invite = await db.InviteCodes.SingleAsync(c => c.Code == adminCode);
        invite.Role.Should().Be(UserRole.Admin);

        // Subjects are no longer seeded — the admin adds them through the bot.
        (await db.Subjects.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Seeding_is_idempotent_across_restarts()
    {
        await using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [DatabaseInitializer.BootstrapAdminCodeKey] = "IDEMPOTENT-CODE",
        });

        var initializer = ActivatorUtilities.CreateInstance<DatabaseInitializer>(provider);
        await initializer.StartAsync(CancellationToken.None);
        await initializer.StartAsync(CancellationToken.None);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTrackDbContext>();

        (await db.InviteCodes.CountAsync(c => c.Code == "IDEMPOTENT-CODE")).Should().Be(1);
    }

    private ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<ILogger<DatabaseInitializer>>(NullLogger<DatabaseInitializer>.Instance)
            .AddDbContext<EduTrackDbContext>(options =>
                options.UseNpgsql(_container.GetConnectionString(), npgsql =>
                    npgsql.MigrationsAssembly(typeof(EduTrackDbContext).Assembly.FullName)))
            .BuildServiceProvider();
    }
}
