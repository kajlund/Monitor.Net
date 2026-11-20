using System.Globalization;
using Microsoft.Extensions.Options;

namespace Monitor.Features.Backups;

public class BackupChecker(IOptions<BackupOptions> options)
{
  private readonly BackupOptions _options = options.Value;

  public BackupStatus Check()
  {
    if (!OperatingSystem.IsLinux())
    {
      return new BackupStatus("unknown", "Requires Linux");
    }

    var root = _options.SnapshotsPath;

    try
    {
      var latestLink = new DirectoryInfo(
    Path.Combine(root, "latest"));

      var latest = latestLink.ResolveLinkTarget(
        returnFinalTarget: true);

      if (latest is null || !latest.Exists)
      {
        return new BackupStatus("warn", "No successful backup found");
      }

      if (!DateTime.TryParseExact(
        latest.Name,
        "yyyy-MM-dd_HHmmss",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var backupDate))
      {
        return new BackupStatus("warn", "Invalid backup timestamp");
      }

      var age = DateTime.Now - backupDate;
      var days = Math.Max(0, (int)Math.Floor(age.TotalDays));

      return new BackupStatus(
        days > _options.WarningAgeDays ? "warn" : "ok",
        $"{backupDate:dd.MM.yyyy HH:mm} · {days}d ago");
    }
    catch (IOException)
    {
      return new BackupStatus("warn", "Backup status unavailable");
    }
    catch (UnauthorizedAccessException)
    {
      return new BackupStatus("warn", "Backup status unavailable");
    }
  }

  public BackupSnapshot[] GetSnapshots()
  {
    if (!OperatingSystem.IsLinux())
    {
      return [];
    }

    var root = _options.SnapshotsPath;

    try
    {
      var latestLink = new DirectoryInfo(
        Path.Combine(root, "latest"));

      var latest = latestLink.ResolveLinkTarget(
        returnFinalTarget: true);

      return new DirectoryInfo(root)
        .EnumerateDirectories()
        .Where(directory => !directory.Attributes.HasFlag(
          FileAttributes.ReparsePoint))
        .Select(directory =>
        {
          if (!DateTime.TryParseExact(
            directory.Name,
            "yyyy-MM-dd_HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var createdAt))
          {
            return null;
          }

          return new BackupSnapshot(
            directory.Name,
            createdAt,
            latest is not null &&
              Path.GetFullPath(directory.FullName) ==
              Path.GetFullPath(latest.FullName));
        })
        .OfType<BackupSnapshot>()
        .OrderByDescending(snapshot => snapshot.CreatedAt)
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
}