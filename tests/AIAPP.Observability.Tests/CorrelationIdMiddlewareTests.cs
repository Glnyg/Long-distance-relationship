using System.Diagnostics;
using AIAPP.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIAPP.Observability.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_reuses_valid_correlation_id_and_sets_trace_tag()
    {
        using var activity = new Activity("request").Start();
        var context = new DefaultHttpContext();
        context.Request.Headers[AiAppTelemetryNames.CorrelationIdHeaderName] = "client-request-123";

        var middleware = new CorrelationIdMiddleware(
            next: httpContext =>
            {
                Assert.Equal("client-request-123", httpContext.Items[AiAppTelemetryNames.CorrelationIdItemName]);
                return Task.CompletedTask;
            },
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("client-request-123", context.Response.Headers[AiAppTelemetryNames.CorrelationIdHeaderName]);
        Assert.Equal("client-request-123", activity.GetTagItem(AiAppTelemetryNames.Tags.CorrelationId));
    }

    [Fact]
    public async Task InvokeAsync_replaces_invalid_correlation_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[AiAppTelemetryNames.CorrelationIdHeaderName] = "bad id with spaces";

        var middleware = new CorrelationIdMiddleware(
            next: _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var generated = context.Response.Headers[AiAppTelemetryNames.CorrelationIdHeaderName].ToString();
        Assert.Matches("^[a-f0-9]{32}$", generated);
    }
}
