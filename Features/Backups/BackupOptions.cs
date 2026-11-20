namespace Monitor.Features.Backups;

public class BackupOptions
{
  public int ExecutionHistoryLimit { get; set; } = 10;
  public string RootPath { get; set; } = "/srv/backup";

  public string SnapshotsDirectory { get; set; } = "snapshots";

  public string LogsDirectory { get; set; } = "logs";

  public int WarningAgeDays { get; set; } = 14;

  public string SnapshotsPath =>
    Path.Combine(RootPath, SnapshotsDirectory);

  public string LogsPath =>
    Path.Combine(RootPath, LogsDirectory);

  public string ServiceName { get; set; } =
    "dreamquest-backup-storage.service";

  public int CommandTimeoutSeconds { get; set; } = 5;
}