using EduTrack.Application.Common.Time;

namespace EduTrack.Integration.Tests.TestSupport;

public sealed class FixedClock : IDateTimeProvider
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}
