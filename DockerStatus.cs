public record DockerStatus(
    string Status,
    string Message,
    ContainerStatus[] Containers);