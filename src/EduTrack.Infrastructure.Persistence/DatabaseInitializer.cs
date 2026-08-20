using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EduTrack.Infrastructure.Persistence;

/// <summary>
/// On startup: applies pending EF Core migrations and seeds a bootstrap admin invite code.
/// </summary>
public sealed class DatabaseInitializer : IHostedService
{
    public const string BootstrapAdminCodeKey = "Bootstrap:AdminInviteCode";

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
}
