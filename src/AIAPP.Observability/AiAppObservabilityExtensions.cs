using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AIAPP.Observability;

public static class AiAppObservabilityExtensions
{
    public static IServiceCollection AddAiAppObservability(
        this IServiceCollection services,
        Action<AiAppObservabilityOptions>? configure = null)
    {
        var options = new AiAppObservabilityOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<TelemetrySanitizer>();
        services.AddHealthChecks();
        services.Configure<OpenTelemetryLoggerOptions>(logging =>
        {
            logging.IncludeScopes = true;
            logging.IncludeFormattedMessage = false;
        });

        var healthCheckPath = new PathString(options.HealthCheckPath);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(instrumentationOptions =>
                    {
                        instrumentationOptions.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments(healthCheckPath);
                    })
                    .AddHttpClientInstrumentation(instrumentationOptions =>
                    {
                        instrumentationOptions.RecordException = true;
                    });

                foreach (var sourceName in options.ActivitySourceNames)
                {
                    tracing.AddSource(sourceName);
                }

                tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                foreach (var meterName in options.MeterNames)
                {
                    metrics.AddMeter(meterName);
                }

                metrics.AddOtlpExporter();
            })
            .WithLogging(logging =>
            {
                logging.AddOtlpExporter();
            });

        return services;
    }

    public static IApplicationBuilder UseAiAppCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
