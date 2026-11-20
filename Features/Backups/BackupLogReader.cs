using System.Globalization;
using Microsoft.Extensions.Options;

namespace Monitor.Features.Backups;

public class BackupLogReader(IOptions<BackupOptions> options)
{
  private readonly BackupOptions _options = options.Value;

  public BackupLog? GetLatestLog()
  {
    if (!OperatingSystem.IsLinux())
    {
      return null;
    }

    try
    {
      var latest = Directory
        .EnumerateFiles(_options.LogsPath, "backup-*.log")
        .OrderByDescending(Path.GetFileName)
        .FirstOrDefault();

      if (latest is null)
      {
        return null;
      }

      return ReadLog(latest);
    }
    catch (IOException)
    {
      return null;
    }
    catch (UnauthorizedAccessException)
    {
      return null;
    }
  }

  public BackupLog? GetLog(string executionName)
  {
    if (!OperatingSystem.IsLinux() ||
        !System.Text.RegularExpressions.Regex.IsMatch(
          executionName,
          @"^backup-\d{4}-\d{2}-\d{2}_\d{6}$"))
    {
      return null;
    }

    var path = Path.Combine(
      _options.LogsPath,
      executionName + ".log");

    return ReadLog(path);
  }

  private static BackupLog? ReadLog(string path)
  {
    try
    {
      if (!File.Exists(path))
      {
        return null;
      }

      var name = Path.GetFileNameWithoutExtension(path);
      var timestamp = name["backup-".Length..];

      if (!DateTime.TryParseExact(
        timestamp,
        "yyyy-MM-dd_HHmmss",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var startedAt))
      {
        return null;
      }

      const int maxLogBytes = 64 * 1024;

      using var stream = new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite);

      var bytesToRead = (int)Math.Min(stream.Length, maxLogBytes);

      stream.Seek(-bytesToRead, SeekOrigin.End);

      var buffer = new byte[bytesToRead];
      stream.ReadExactly(buffer);

      var content = System.Text.Encoding.UTF8.GetString(buffer);

      if (stream.Length > maxLogBytes)
      {
        var firstNewline = content.IndexOf('\n');

        if (firstNewline >= 0)
        {
          content = content[(firstNewline + 1)..];
        }

        content = "[Showing the end of the log]\n\n" + content;
      }

      var resultPath = Path.ChangeExtension(path, ".result");
      var status = ReadResultStatus(resultPath);

      return new BackupLog(
        Path.GetFileName(path),
        startedAt,
        status,
        content);
    }
    catch (IOException)
    {
      return null;
    }
    catch (UnauthorizedAccessException)
    {
      return null;
    }
  }

  private static string ReadResultStatus(string resultPath)
  {
    try
    {
      if (!File.Exists(resultPath))
      {
        return "unknown";
      }

      foreach (var line in File.ReadLines(resultPath))
      {
        if (!line.StartsWith("status=", StringComparison.Ordinal))
        {
          continue;
        }

        var value = line["status=".Length..].Trim();

        return value switch
        {
          "success" => "ok",
          "failed" => "error",
          "running" => "warn",
          _ => "unknown"
        };
      }
    }
    catch (IOException)
    {
      return "unknown";
    }
    catch (UnauthorizedAccessException)
    {
      return "unknown";
    }

    return "unknown";
  }
}
