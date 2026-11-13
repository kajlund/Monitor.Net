namespace Monitor.Features.Backups;

public class BackupChecker
{
  public BackupStatus Check()
  {
    if (!OperatingSystem.IsLinux())
    {
      return new BackupStatus("unknown", "Requires Linux");
    }

    const string root = "/srv/backup/snapshots";

    try
    {
      var latest = new DirectoryInfo(root)
          .EnumerateDirectories()
          .OrderByDescending(directory => directory.LastWriteTimeUtc)
          .FirstOrDefault();

      if (latest is null)
      {
        return new BackupStatus("warn", "No backup found");
      }

      var age = DateTime.UtcNow - latest.LastWriteTimeUtc;
      var days = Math.Max(0, (int)Math.Floor(age.TotalDays));

      return new BackupStatus(
          days > 14 ? "warn" : "ok",
          $"{latest.Name} · {days}d ago");
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
}