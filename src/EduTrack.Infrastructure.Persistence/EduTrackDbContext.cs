using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace EduTrack.Infrastructure.Persistence;

/// <summary>
/// Root EF Core context for the EduTrack modular monolith.
/// </summary>
public class EduTrackDbContext : DbContext
{
    public EduTrackDbContext(DbContextOptions<EduTrackDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
