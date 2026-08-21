using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EduTrack.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IHostedService
{
    public const string BootstrapAdminCodeKey = "Bootstrap:AdminInviteCode";
    public const string BootstrapSubjectsKey = "Bootstrap:Subjects";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTrackDbContext>();

        _logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(cancellationToken);

        await SeedBootstrapAdminCodeAsync(db, cancellationToken);
        await SeedBootstrapSubjectsAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedBootstrapAdminCodeAsync(EduTrackDbContext db, CancellationToken cancellationToken)
    {
        var code = _configuration[BootstrapAdminCodeKey];
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var exists = await db.InviteCodes.AnyAsync(c => c.Code == code, cancellationToken);
        if (exists)
        {
            return;
        }

        db.InviteCodes.Add(InviteCode.Create(code, UserRole.Admin, DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded bootstrap admin invite code.");
    }

    private async Task SeedBootstrapSubjectsAsync(EduTrackDbContext db, CancellationToken cancellationToken)
    {
        var names = _configuration.GetSection(BootstrapSubjectsKey)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        if (names.Length == 0)
        {
            return;
        }

        if (await db.Subjects.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var name in names)
        {
            db.Subjects.Add(Subject.Create(name, null, now));
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} bootstrap subjects.", names.Length);
    }
}
