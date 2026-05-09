using System.Diagnostics;

namespace AIAPP.ConsentService.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable())
        {
            Skip = "Docker daemon 不可用，跳过需要 Testcontainers PostgreSQL 的集成测试。";
        }
    }
}

internal static class DockerAvailability
{
    private static readonly Lazy<bool> Availability = new(CheckDocker);

    public static bool IsAvailable()
    {
        return Availability.Value;
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
                ArgumentList = { "info", "--format", "{{json .ServerVersion}}" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(milliseconds: 3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // 进程已退出时无需处理；这里不能让 Docker 探测本身影响测试结果。
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
