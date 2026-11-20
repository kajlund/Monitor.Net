namespace Monitor.Features.Backups;

public record BackupExecution(
  string Name,
  DateTimeOffset StartedAt,
  DateTimeOffset? FinishedAt,
  string Status,
  int? ExitCode
);
