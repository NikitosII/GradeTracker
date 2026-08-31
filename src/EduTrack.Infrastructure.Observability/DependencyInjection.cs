using EduTrack.Application.Abstractions.Observability;
using EduTrack.Infrastructure.Observability.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace EduTrack.Infrastructure.Observability;

public static class DependencyInjection
{
    /// <summary>
    /// Wires structured JSON logging (Serilog), distributed tracing and metrics
    /// (OpenTelemetry), and the application metrics recorder. Host-specific instrumentation
    /// (e.g. ASP.NET Core) is supplied through the optional callbacks.
    /// </summary>
    public static TBuilder AddEduTrackObservability<TBuilder>(
        this TBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
        where TBuilder : IHostApplicationBuilder
    {
        var options = builder.Configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        ConfigureLogging(builder, serviceName);
        ConfigureMetricsAndTracing(builder, serviceName, options, configureTracing, configureMetrics);

        return builder;
    }

    private static void ConfigureLogging<TBuilder>(TBuilder builder, string serviceName)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.With<ActivityEnricher>()
            .WriteTo.Console(new CompactJsonFormatter()));
    }

    private static void ConfigureMetricsAndTracing<TBuilder>(
        TBuilder builder,
        string serviceName,
        ObservabilityOptions options,
        Action<TracerProviderBuilder>? configureTracing,
        Action<MeterProviderBuilder>? configureMetrics)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<IApplicationMetrics, ApplicationMetrics>();

        var hasOtlp = !string.IsNullOrWhiteSpace(options.OtlpEndpoint);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(EduTrackTelemetry.ActivitySourceName)
                    .AddSource("MassTransit")
                    .AddSource("Npgsql")
                    .AddHttpClientInstrumentation();

                configureTracing?.Invoke(tracing);

                if (hasOtlp)
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(options.OtlpEndpoint!));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(EduTrackTelemetry.MeterName)
                    .AddMeter("MassTransit")
                    .AddMeter("Npgsql")
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                configureMetrics?.Invoke(metrics);

                if (options.PrometheusEnabled)
                {
                    metrics.AddPrometheusHttpListener(listener =>
                        listener.UriPrefixes = new[] { options.PrometheusHttpListenerPrefix });
                }

                if (hasOtlp)
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(options.OtlpEndpoint!));
                }
            });
    }
}
