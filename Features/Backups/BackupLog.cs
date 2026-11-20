namespace Monitor.Features.Backups;

public record BackupLog(
  string FileName,
  DateTime StartedAt,
  string Status,
  string Content
);