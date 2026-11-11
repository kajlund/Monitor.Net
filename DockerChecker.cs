using System.Diagnostics;

public class DockerChecker
{
    public async Task<DockerStatus> CheckAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return new DockerStatus("unknown", "Requires Linux", []);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/docker",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("ps");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("{{.Names}}|{{.Status}}");

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

                return new DockerStatus("error", "Docker check timed out", []);
            }

            await Task.WhenAll(output, errors);

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine(
                    $"Docker check failed: {await errors}");

                return new DockerStatus("error", "Docker unavailable", []);
            }

            var lines = (await output).Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            var containers = new List<ContainerStatus>();

            foreach (var line in lines)
            {
                var parts = line.Split('|', 2);

                if (parts.Length != 2)
                {
                    return new DockerStatus(
                        "error", "Unexpected Docker output", []);
                }

                var unhealthy = parts[1].Contains(
                    "unhealthy",
                    StringComparison.OrdinalIgnoreCase);

                containers.Add(new ContainerStatus(
                    parts[0],
                    unhealthy ? "error" : "ok",
                    parts[1]));
            }

            var status = containers.Any(container =>
                container.Status == "error") ? "error" : "ok";

            return new DockerStatus(
                status,
                $"{containers.Count} running",
                containers.ToArray());
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new DockerStatus(
                "error", "Could not start Docker command", []);
        }
    }
}