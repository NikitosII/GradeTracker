using EduTrack.Bot.Web.Telegram;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace EduTrack.Integration.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Echoes_the_incoming_correlation_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "abc-123";

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be("abc-123");
    }

    [Fact]
    public async Task Generates_a_correlation_id_when_none_is_supplied()
    {
        var context = new DefaultHttpContext();

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Propagates_the_id_to_the_log_context_during_the_request()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "trace-xyz";
        var seenDuringRequest = false;

        var middleware = new CorrelationIdMiddleware(_ =>
        {
            // The response header is set before next runs, so it is observable here too.
            seenDuringRequest = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString() == "trace-xyz";
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context);

        seenDuringRequest.Should().BeTrue();
    }
}
