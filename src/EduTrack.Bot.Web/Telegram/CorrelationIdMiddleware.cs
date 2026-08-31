using System.Diagnostics;
using Serilog.Context;

namespace EduTrack.Bot.Web.Telegram;

/// <summary>
/// Gives every request a correlation id.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Resolve(context);

        Activity.Current?.SetTag("correlation_id", correlationId);

        // This middleware runs first, so the response has not started: setting the header
        // now is safe and echoes the id back to the caller.
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string Resolve(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var provided)
            && !string.IsNullOrWhiteSpace(provided))
        {
            return provided.ToString();
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
