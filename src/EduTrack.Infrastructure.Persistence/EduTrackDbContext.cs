using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Domain.Audit;
using EduTrack.Domain.Inbox;
using EduTrack.Domain.Notifications;
using EduTrack.Domain.Outbox;
using EduTrack.Domain.Reminders;
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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
