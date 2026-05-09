using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AIAPP.Observability;

/// <summary>
/// 服务端可观测性的统一入口。新服务只需要调用这里，就能同时接入日志、Trace、Metric 和健康检查。
/// </summary>
public static class AiAppObservabilityExtensions
{
    /// <summary>
    /// 注册 AIAPP 统一可观测性能力。以后新增 ASP.NET Core 服务时优先复制这个调用方式，不要在各服务里散落配置。
    /// </summary>
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

        // 日志只导出结构化字段；不导出格式化后的整段消息，降低敏感文本误入日志系统的风险。
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
                        // 健康检查请求很多但排障价值低，过滤掉可以让 Trace 更清爽。
                        instrumentationOptions.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments(healthCheckPath);
                    })
                    .AddHttpClientInstrumentation(instrumentationOptions =>
                    {
                        instrumentationOptions.RecordException = true;
                    });

                foreach (var sourceName in options.ActivitySourceNames)
                {
                    // ActivitySource 名称必须注册，代码里 StartActivity 产生的 span 才会被 OpenTelemetry 采集。
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
                    // Meter 名称必须注册，自定义业务指标才会被导出到 Prometheus 等系统。
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

    /// <summary>
    /// 给每个 HTTP 请求补充 correlation_id，用来把客户端、服务端、AI 调用和推送串成一条排障链路。
    /// </summary>
    public static IApplicationBuilder UseAiAppCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
