using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Monitor.Features.Backups;

public class BackupRunner(
  IOptions<BackupOptions> options,
  ILogger<BackupRunner> logger)
{
  private readonly BackupOptions _options = options.Value;

  public async Task<BackupStartResult> StartAsync()
  {
    if (!OperatingSystem.IsLinux())
    {
      return new BackupStartResult(false, "Requires Linux");
    }

    var startInfo = new ProcessStartInfo
    {
      FileName = "/usr/bin/sudo",
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    startInfo.ArgumentList.Add("-n");
    startInfo.ArgumentList.Add("/usr/bin/systemctl");
    startInfo.ArgumentList.Add("start");
    startInfo.ArgumentList.Add("--no-block");
    startInfo.ArgumentList.Add(_options.ServiceName);

    try
    {
      using var process = new Process { StartInfo = startInfo };

      using var timeout = new CancellationTokenSource(
        TimeSpan.FromSeconds(_options.CommandTimeoutSeconds));

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
          await process.WaitForExitAsync();
        }

        logger.LogWarning(
          "Timed out requesting backup service start.");

        return new BackupStartResult(
          false,
          "Backup start request timed out. Check service status before retrying.");
      }

      await Task.WhenAll(output, errors);

      if (process.ExitCode != 0)
      {
        logger.LogError(
          "Backup request failed: {Error}", await errors);

        return new BackupStartResult(
          false, "Could not start backup service");
      }

      return new BackupStartResult(
        true, "Backup request accepted");
    }
    catch (System.ComponentModel.Win32Exception ex)
    {
      logger.LogError(ex, "Could not execute backup command");

      return new BackupStartResult(
        false, "Could not execute backup command");
    }
  }
  public async Task<BackupServiceState> GetStateAsync()
  {
    if (!OperatingSystem.IsLinux())
    {
      return BackupServiceState.Unknown;
    }

    var startInfo = new ProcessStartInfo
    {
      FileName = "/usr/bin/systemctl",
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    startInfo.ArgumentList.Add("show");
    startInfo.ArgumentList.Add("--property=ActiveState");
    startInfo.ArgumentList.Add("--value");
    startInfo.ArgumentList.Add(_options.ServiceName);

    try
    {
      using var process = new Process { StartInfo = startInfo };

      using var timeout = new CancellationTokenSource(
        TimeSpan.FromSeconds(_options.CommandTimeoutSeconds));

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

        return BackupServiceState.Unknown;
      }

      await Task.WhenAll(output, errors);

      if (process.ExitCode != 0)
      {
        return BackupServiceState.Unknown;
      }

      var state = (await output).Trim();

      return state switch
      {
        "inactive" => BackupServiceState.Idle,
        "activating" => BackupServiceState.Running,
        "active" => BackupServiceState.Running,
        "deactivating" => BackupServiceState.Running,
        "failed" => BackupServiceState.Failed,
        _ => BackupServiceState.Unknown
      };
    }
    catch (System.ComponentModel.Win32Exception ex)
    {
      logger.LogError(ex, "Could not query backup service");

      return BackupServiceState.Unknown;
    }
  }
}