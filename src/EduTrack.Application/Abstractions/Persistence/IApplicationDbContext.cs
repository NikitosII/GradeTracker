using EduTrack.Domain.Audit;
using EduTrack.Domain.Notifications;
using EduTrack.Domain.Outbox;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Abstractions.Persistence;

/// <summary>
/// Application-facing view of the database.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<InviteCode> InviteCodes { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<StudentProfile> StudentProfiles { get; }
    DbSet<Grade> Grades { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
