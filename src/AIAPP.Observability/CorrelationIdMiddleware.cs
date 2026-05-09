using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AIAPP.Observability;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = GetOrCreateCorrelationId(context);
        context.Items[AiAppTelemetryNames.CorrelationIdItemName] = correlationId;
        context.Response.Headers[AiAppTelemetryNames.CorrelationIdHeaderName] = correlationId;

        Activity.Current?.SetTag(AiAppTelemetryNames.Tags.CorrelationId, correlationId);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            [AiAppTelemetryNames.Tags.CorrelationId] = correlationId
        });

        await _next(context).ConfigureAwait(false);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers.TryGetValue(AiAppTelemetryNames.CorrelationIdHeaderName, out var values)
            ? values.FirstOrDefault()
            : null;

        if (IsValidCorrelationId(incoming))
        {
            return incoming!.Trim();
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsValidCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is < 8 or > 64)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            if (!char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.' and not ':')
            {
                return false;
            }
        }

        return true;
    }
}
