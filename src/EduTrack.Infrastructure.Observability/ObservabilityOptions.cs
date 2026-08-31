namespace EduTrack.Infrastructure.Observability;

/// <summary>
/// Configures where telemetry goes. 
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// OTLP endpoint for traces and metrics (e.g. http://otel-collector:4317). When empty,
    /// nothing is exported over OTLP.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    public bool PrometheusEnabled { get; set; }
    public string PrometheusHttpListenerPrefix { get; set; } = "http://+:9464/";
}
