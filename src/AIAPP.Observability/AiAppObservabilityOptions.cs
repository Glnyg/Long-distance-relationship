namespace AIAPP.Observability;

/// <summary>
/// 每个服务接入可观测性时可以调整的选项。大多数服务只需要设置 ServiceName。
/// </summary>
public sealed class AiAppObservabilityOptions
{
    // 显示在 Grafana、Tempo、Loki 中的服务名，必须稳定，便于排查问题。
    public string ServiceName { get; set; } = "AIAPP.UnknownService";

    // 健康检查路径会被 Trace 过滤，避免大量无业务意义的探活请求污染链路。
    public string HealthCheckPath { get; set; } = "/healthz";

    // 这里列出需要采集的 ActivitySource。新增服务后要把自己的 Source 放进统一名称表。
    public IList<string> ActivitySourceNames { get; } =
        new List<string>(AiAppTelemetryNames.ActivitySources.All);

    // 这里列出需要采集的 Meter。新增业务指标后要保证 Meter 名称在这里注册。
    public IList<string> MeterNames { get; } =
        new List<string>(AiAppTelemetryNames.Meters.All);

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new InvalidOperationException("ServiceName must be configured for observability.");
        }

        if (string.IsNullOrWhiteSpace(HealthCheckPath) || !HealthCheckPath.StartsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("HealthCheckPath must start with '/'.");
        }
    }
}
