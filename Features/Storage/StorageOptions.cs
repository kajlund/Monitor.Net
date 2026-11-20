
namespace Monitor.Features.Storage;

public class StorageOptions
{
  public List<StorageMountOptions> Mounts { get; set; } = [];

  public int WarningPercent { get; set; } = 80;

  public int ErrorPercent { get; set; } = 90;

  public int CommandTimeoutSeconds { get; set; } = 5;
}

public class StorageMountOptions
{
  public string Path { get; set; } = "";

  public string Label { get; set; } = "";
}
