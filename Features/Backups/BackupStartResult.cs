namespace Monitor.Features.Backups;

public record BackupStartResult(
  bool Accepted,
  string Message
);