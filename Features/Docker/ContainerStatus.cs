namespace Monitor.Features.Docker;

public record ContainerStatus(
  string Name,
  string Status,
  string State);
