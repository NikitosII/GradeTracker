using System.Diagnostics;

namespace EduTrack.Infrastructure.Observability;

/// <summary>
/// Well-known names for the application's own tracing and metrics sources.
/// </summary>
public static class EduTrackTelemetry
{
    /// <summary>Name of the application's <see cref="System.Diagnostics.Metrics.Meter"/>.</summary>
    public const string MeterName = "EduTrack";

    /// <summary>Name of the application's <see cref="System.Diagnostics.ActivitySource"/>.</summary>
    public const string ActivitySourceName = "EduTrack";

    /// <summary>Shared source for spans the application starts itself (e.g. update handling).</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
