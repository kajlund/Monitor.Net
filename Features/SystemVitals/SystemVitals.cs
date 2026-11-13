namespace Monitor.Features.SystemVitals;

public record SystemVitals(
  string MachineName,
  string Uptime,
  string Temperature = "n/a",
  string Memory = "n/a",
  string Load = "n/a",
  string LoadDetail = "");