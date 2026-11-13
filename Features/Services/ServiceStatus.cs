namespace Monitor.Features.Services;

public record ServiceStatus(
    string Label,
    string Status,
    string Message);