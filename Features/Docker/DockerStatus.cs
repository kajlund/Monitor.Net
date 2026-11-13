namespace Monitor.Features.Docker;

public record DockerStatus(
  string Status,
  string Message,
  ContainerStatus[] Containers);
