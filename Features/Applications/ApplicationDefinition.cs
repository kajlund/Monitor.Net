namespace Monitor.Features.Applications;

public class ApplicationDefinition
{
  public string Name { get; set; } = "";
  public string HealthUrl { get; set; } = "";
  public string PublicUrl { get; set; } = "";
  public string Description { get; set; } = "";
  public string IconUrl { get; set; } = "";
}
