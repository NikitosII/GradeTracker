using EduTrack.Application.Abstractions.Persistence;
using EduTrack.Application.Common.Time;
using EduTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Application.Tests.TestSupport;

public sealed class TestApplicationDbContext : DbContext, IApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();

    public static TestApplicationDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase($"edutrack-tests-{Guid.NewGuid()}")
            .Options);
}

public sealed class FixedClock : IDateTimeProvider
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}
