using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EduTrack.Infrastructure.Persistence;

public class EduTrackDbContextFactory : IDesignTimeDbContextFactory<EduTrackDbContext>
{
    public EduTrackDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=edutrack;Username=edutrack;Password=edutrack";

        var options = new DbContextOptionsBuilder<EduTrackDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(EduTrackDbContext).Assembly.FullName))
            .Options;

        return new EduTrackDbContext(options);
    }
}
