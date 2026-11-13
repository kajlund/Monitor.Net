namespace Monitor.Features.Storage;

public record StorageStatus(
    string Label,
    string Path,
    string Status,
    string Message
);