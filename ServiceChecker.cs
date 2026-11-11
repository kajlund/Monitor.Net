using System.Diagnostics;

public class ServiceChecker
{
    public async Task<ServiceStatus[]> CheckAsync()
    {
        return await Task.WhenAll(
            CheckUnitAsync("Caddy", "caddy.service"),
            CheckUnitAsync("Samba", "smbd.service"),
            CheckUnitAsync("MongoDB", "mongod.service"),
            CheckUnitAsync("PostgreSQL", "postgresql.service"),
            CheckUnitAsync("Docker", "docker.service"),
            CheckProcessAsync("Syncthing", "syncthing"));
    }

    private static Task<ServiceStatus> CheckUnitAsync(
        string label,
        string unit)
    {
        return InspectAsync(
            label,
            "/usr/bin/systemctl",
            ["is-active", unit],
            isProcessCheck: false);
    }

    private static Task<ServiceStatus> CheckProcessAsync(
        string label,
        string processName)
    {
        return InspectAsync(
            label,
            "/usr/bin/pgrep",
            ["-x", processName],
            isProcessCheck: true);
    }

    private static async Task<ServiceStatus> InspectAsync(
        string label,
        string command,
        string[] arguments,
        bool isProcessCheck)
    {
        if (!OperatingSystem.IsLinux())
        {
            return new ServiceStatus(label, "unknown", "Requires Linux");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.Environment["LC_ALL"] = "C";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(5));

            process.Start();

            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync();
                await Task.WhenAll(output, errors);

                return new ServiceStatus(label, "error", "Check timed out");
            }

            await Task.WhenAll(output, errors);
            var text = (await output).Trim();

            if (isProcessCheck)
            {
                return process.ExitCode switch
                {
                    0 => new ServiceStatus(label, "ok", "running"),
                    1 => new ServiceStatus(label, "error", "not running"),
                    _ => new ServiceStatus(label, "error", "Process check failed")
                };
            }

            var active = process.ExitCode == 0 && text == "active";

            return new ServiceStatus(
                label,
                active ? "ok" : "error",
                string.IsNullOrWhiteSpace(text) ? "Check failed" : text);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new ServiceStatus(label, "error", "Could not start command");
        }
    }
}