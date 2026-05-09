namespace AIAPP.Observability;

public sealed class AiAppObservabilityOptions
{
    public string ServiceName { get; set; } = "AIAPP.UnknownService";

    public string HealthCheckPath { get; set; } = "/healthz";

    public IList<string> ActivitySourceNames { get; } =
        new List<string>(AiAppTelemetryNames.ActivitySources.All);

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
