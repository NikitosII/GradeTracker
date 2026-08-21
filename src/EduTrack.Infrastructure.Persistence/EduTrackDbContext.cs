using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace EduTrack.Infrastructure.Persistence;

/// <summary>
/// Root EF Core context for the EduTrack modular monolith.
/// </summary>
public class EduTrackDbContext : DbContext, IApplicationDbContext
{
    public EduTrackDbContext(DbContextOptions<EduTrackDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Assignment> Assignments => Set<Assignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
