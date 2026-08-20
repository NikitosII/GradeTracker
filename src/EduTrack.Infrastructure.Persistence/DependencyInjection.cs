using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EduTrack.Infrastructure.Persistence;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    /// <summary>
    /// Registers the EF Core PostgreSQL context and a readiness health check that verifies database connectivity.
    /// </summary>
    public static IServiceCollection AddPersistenceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<EduTrackDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(EduTrackDbContext).Assembly.FullName)));

        services.AddHealthChecks()
            .AddDbContextCheck<EduTrackDbContext>(
                name: "postgres",
                tags: new[] { "ready" });

        return services;
    }
}
