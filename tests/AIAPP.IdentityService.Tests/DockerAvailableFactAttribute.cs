using System.Diagnostics;

namespace AIAPP.IdentityService.Tests;

/// <summary>
/// 这些测试依赖 PostgreSQL Testcontainers。Docker daemon 不可用时跳过，避免本地环境问题被误判成业务失败。
/// </summary>
public sealed class DockerAvailableFactAttribute : FactAttribute
{
    public DockerAvailableFactAttribute()
    {
        if (!DockerAvailability.IsDockerAvailable())
        {
            Skip = "Docker daemon 不可用，跳过 PostgreSQL Testcontainers 集成测试。";
        }
    }
}

internal static class DockerAvailability
{
    private static readonly Lazy<bool> Available = new(CheckDocker);

    public static bool IsDockerAvailable()
    {
        return Available.Value;
    }

    private static bool CheckDocker()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AIAPP_RUN_TESTCONTAINERS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList = { "info", "--format", "{{.ServerVersion}}" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            return process.WaitForExit(5_000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
