namespace Monitor.Features.Backups;

public record BackupSnapshot(
  string Name,
  DateTime CreatedAt,
  bool IsLatest
);