using EduTrack.Application.Abstractions.Telegram;
using EduTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduTrack.Integration.Tests.TestSupport;

/// <summary>
/// Boots the real <c>EduTrack.Bot.Web</c> host against a throwaway Postgres
/// container. The Telegram sender is swapped for a recording double and the
/// webhook URL is left blank so nothing reaches the Telegram Bot API. Migrations
/// are applied by the app's own <c>DatabaseInitializer</c> on startup.
/// </summary>
public sealed class EduTrackWebAppFactory : WebApplicationFactory<Program>
{
    public const string WebhookSecret = "integration-test-secret";

    private readonly string _connectionString;

    public EduTrackWebAppFactory(string connectionString) => _connectionString = connectionString;

    public RecordingTelegramSender TelegramSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = _connectionString,
                ["Telegram:BotToken"] = "123456:TEST-TOKEN",
                ["Telegram:WebhookSecret"] = WebhookSecret,
                ["Telegram:WebhookUrl"] = string.Empty,
                ["Bootstrap:AdminInviteCode"] = string.Empty,
            });
        });

        builder.ConfigureServices(services =>
        {
            // AddPersistenceInfrastructure reads the connection string eagerly at
            // startup (before the config override above merges), so re-point the
            // DbContext at the container here, where the registration wins.
            services.RemoveAll<DbContextOptions<EduTrackDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<EduTrackDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(EduTrackDbContext).Assembly.FullName)));

            services.RemoveAll<ITelegramSender>();
            services.AddSingleton<ITelegramSender>(TelegramSender);
        });
    }

    /// <summary>Runs an action inside a fresh DI scope with the real EF Core context.</summary>
    public async Task WithDbContextAsync(Func<EduTrackDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTrackDbContext>();
        await action(db);
    }

    /// <summary>Runs a function inside a fresh DI scope with the real EF Core context.</summary>
    public async Task<T> WithScopeAsync<T>(Func<EduTrackDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTrackDbContext>();
        return await action(db);
    }
}
