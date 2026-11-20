using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace Monitor.Features.Storage;

public class StorageChecker(IOptions<StorageOptions> options)
{

  private readonly StorageOptions _options = options.Value;
  public async Task<StorageStatus[]> CheckAsync()
  {
    return await Task.WhenAll(
      _options.Mounts.Select(mount =>
        InspectAsync(mount.Path, mount.Label)));
  }

  private async Task<StorageStatus> InspectAsync(
      string path,
      string label)
  {
    if (!OperatingSystem.IsLinux())
    {
      return new StorageStatus(
          label, path, "unknown", "Requires Linux");
    }

    try
    {
      var mount = await RunAsync(
          "/usr/bin/findmnt",
          "--noheadings", "--mountpoint", path);

      if (mount.ExitCode != 0)
      {
        return new StorageStatus(
            label,
            path,
            "error",
            mount.ExitCode == 1
                ? "Not mounted"
                : "Mount check failed");
      }

      var disk = await RunAsync(
          "/usr/bin/df",
          "--block-size=1",
          "--output=size,used,pcent",
          "--",
          path);

      if (disk.ExitCode != 0)
      {
        return new StorageStatus(
            label, path, "error", "Disk usage check failed");
      }

      var lines = disk.Output.Split(
          '\n',
          StringSplitOptions.RemoveEmptyEntries);

      if (lines.Length != 2)
      {
        return new StorageStatus(
            label, path, "error", "Unexpected disk usage output");
      }

      var parts = lines[1].Split(
          (char[]?)null,
          StringSplitOptions.RemoveEmptyEntries);

      if (parts.Length != 3 ||
          !TryReadNumber(parts[0], out var total) ||
          !TryReadNumber(parts[1], out var used) ||
          !TryReadNumber(parts[2].TrimEnd('%'), out var percent) ||
          total <= 0)
      {
        return new StorageStatus(
            label, path, "error", "Invalid disk usage values");
      }

      var status = percent >= _options.ErrorPercent ? "error"
    : percent >= _options.WarningPercent ? "warn"
    : "ok";

      var unitSize = total >= Math.Pow(1024, 4)
        ? Math.Pow(1024, 4)
        : Math.Pow(1024, 3);

      var unit = total >= Math.Pow(1024, 4) ? "TiB" : "GiB";

      return new StorageStatus(
          label,
          path,
          status,
          $"{used / unitSize:F1} / {total / unitSize:F1} {unit} ({percent}%)");
    }
    catch (OperationCanceledException)
    {
      return new StorageStatus(
          label, path, "error", "Storage check timed out");
    }
    catch (System.ComponentModel.Win32Exception)
    {
      return new StorageStatus(
          label, path, "error", "Could not start storage command");
    }
  }

  private static bool TryReadNumber(string text, out long value)
  {
    return long.TryParse(
        text,
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out value);
  }

  private async Task<(int ExitCode, string Output)> RunAsync(
    string command,
    params string[] arguments)
  {
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

    using var process = new Process { StartInfo = startInfo };
    using var timeout = new CancellationTokenSource(
        TimeSpan.FromSeconds(_options.CommandTimeoutSeconds));

    process.Start();

    var output = process.StandardOutput.ReadToEndAsync();
    var errors = process.StandardError.ReadToEndAsync();

    try
    {
      await process.WaitForExitAsync(timeout.Token);
      await Task.WhenAll(output, errors);

      return (process.ExitCode, await output);
    }
    catch (OperationCanceledException)
    {
      if (!process.HasExited)
      {
        process.Kill(entireProcessTree: true);
      }

      await process.WaitForExitAsync();
      await Task.WhenAll(output, errors);

      throw;
    }
  }
}