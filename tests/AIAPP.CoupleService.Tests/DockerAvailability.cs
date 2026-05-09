using System.Diagnostics;

namespace AIAPP.CoupleService.Tests;

internal static class DockerAvailability
{
    public static async Task<bool> IsAvailableAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AIAPP_RUN_TESTCONTAINERS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                ArgumentList = { "version", "--format", "{{.Server.Version}}" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var completed = await Task.Run(() => process.WaitForExit(3000));
            return completed && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
