namespace Monitor.Features.Applications;

public record MonitoredApplication(
  string Name,
  string Status,
  long? ResponseTimeMs);