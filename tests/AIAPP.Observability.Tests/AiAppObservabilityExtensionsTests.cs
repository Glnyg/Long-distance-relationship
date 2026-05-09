using AIAPP.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AIAPP.Observability.Tests;

public sealed class AiAppObservabilityExtensionsTests
{
    [Fact]
    public void AddAiAppObservability_registers_shared_services_and_health_checks()
    {
        var services = new ServiceCollection();

        services.AddAiAppObservability(options =>
        {
            options.ServiceName = "AIAPP.TestService";
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<AiAppObservabilityOptions>());
        Assert.NotNull(provider.GetRequiredService<TelemetrySanitizer>());
        Assert.NotNull(provider.GetRequiredService<HealthCheckService>());
    }
}
