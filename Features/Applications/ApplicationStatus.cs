namespace Monitor.Features.Applications;

public record ApplicationStatus(
  ApplicationDefinition Definition,
  MonitoredApplication Check);
