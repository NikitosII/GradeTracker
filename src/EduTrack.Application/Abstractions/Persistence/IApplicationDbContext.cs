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
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
