using System.Globalization;
using Microsoft.Extensions.Options;

namespace Monitor.Features.Backups;

public class BackupExecutionReader(IOptions<BackupOptions> options)
{
  private readonly BackupOptions _options = options.Value;

  public BackupExecution[] GetExecutions()
  {
    if (!OperatingSystem.IsLinux())
    {
      return [];
    }

    try
    {
      return Directory
        .EnumerateFiles(_options.LogsPath, "backup-*.result")
        .Select(ReadExecution)
        .OfType<BackupExecution>()
        .OrderByDescending(execution => execution.StartedAt)
        .ToArray();
    }
    catch (IOException)
    {
      return [];
    }
    catch (UnauthorizedAccessException)
    {
      return [];
    }
  }

  private static BackupExecution? ReadExecution(string path)
  {
    try
    {
      var values = File.ReadLines(path)
        .Select(line => line.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .ToDictionary(parts => parts[0], parts => parts[1]);

      if (!values.TryGetValue("status", out var status) ||
          !values.TryGetValue("started_at", out var startedText) ||
          !DateTimeOffset.TryParse(
            startedText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var startedAt))
      {
        return null;
      }

      DateTimeOffset? finishedAt = null;

      if (values.TryGetValue("finished_at", out var finishedText) &&
          DateTimeOffset.TryParse(
            finishedText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedFinishedAt))
      {
        finishedAt = parsedFinishedAt;
      }

      int? exitCode = null;

      if (values.TryGetValue("exit_code", out var exitText) &&
          int.TryParse(exitText, out var parsedExitCode))
      {
        exitCode = parsedExitCode;
      }

      return new BackupExecution(
        Path.GetFileNameWithoutExtension(path),
        startedAt,
        finishedAt,
        status,
        exitCode);
    }
    catch (IOException)
    {
      return null;
    }
    catch (UnauthorizedAccessException)
    {
      return null;
    }
    catch (ArgumentException)
    {
      return null;
    }
  }
}
