using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitor.Features.Applications;
using Monitor.Features.Backups;
using Monitor.Features.Docker;
using Monitor.Features.Services;
using Monitor.Features.Storage;
using Monitor.Features.SystemVitals;
using Monitor.Models;

public class StatusModel(
    IConfiguration configuration,
    ApplicationChecker checker,
    SystemVitalsReader vitalsReader,
    StorageChecker storageChecker,
    ServiceChecker serviceChecker,
    DockerChecker dockerChecker,
    BackupChecker backupChecker) : PageModel
{
  public MonitoredApplication[] Applications { get; private set; } = [];

  public ApplicationStatus[] ApplicationStatuses { get; private set; } = [];

  public SystemVitals Vitals { get; private set; } = new("", "");

  public StorageStatus[] Storage { get; private set; } = [];

  public ServiceStatus[] Services { get; private set; } = [];

  public DockerStatus Docker { get; private set; } =
    new("unknown", "Not checked", []);

  public BackupStatus Backup { get; private set; } =
    new("unknown", "Not checked");

  public ComponentStatus[] Components { get; private set; } = [];

  public ComponentStatus[] AffectedComponents =>
    Components
      .Where(component =>
          component.Status is "error" or "warn")
      .ToArray();

  public int UnknownCount =>
    Components.Count(component => component.Status == "unknown");

  public string OverallStatus =>
    Components.Any(component => component.Status == "error") ? "error" :
    Components.Any(component => component.Status == "warn") ? "warn" :
    UnknownCount > 0 || Components.Length == 0 ? "unknown" :
      "ok";

  public string SummaryTitle => OverallStatus switch
  {
    "ok" => "All systems healthy",
    "error" or "warn" => "Needs attention",
    _ => "Checks incomplete"
  };

  public string SummaryDetail
  {
    get
    {
      var count = AffectedComponents.Length;

      if (count > 0)
      {
        return count == 1
          ? "1 component needs attention"
          : $"{count} components need attention";
      }

      return OverallStatus == "ok"
        ? "All monitored components are healthy"
        : "Some checks could not run";
    }
  }

  public string SummaryCopy => OverallStatus switch
  {
    "ok" => "No issues were detected by the configured checks.",
    "warn" => "Some items should be reviewed.",
    "error" => "One or more checks reported an error. Review the affected components below.",
    _ => "Some checks require Linux or have not completed."
  };

  public async Task OnGetAsync()
  {
    Vitals = vitalsReader.Read();

    var storageTask = storageChecker.CheckAsync();
    var servicesTask = serviceChecker.CheckAsync();
    var dockerTask = dockerChecker.CheckAsync();

    var definitions = configuration
      .GetSection("Applications")
      .Get<ApplicationDefinition[]>()
      ?? [];

    var checks = definitions.Select(application =>
      checker.CheckAsync(application.Name, application.HealthUrl));

    var applicationsTask = Task.WhenAll(checks);

    Backup = backupChecker.Check();

    await Task.WhenAll(
      storageTask,
      servicesTask,
      dockerTask,
      applicationsTask);

    Storage = await storageTask;
    Services = await servicesTask;
    Docker = await dockerTask;
    Applications = await applicationsTask;

    ApplicationStatuses = definitions
      .Zip(Applications, (definition, check) =>
        new ApplicationStatus(definition, check))
      .ToArray();

    var components = new List<ComponentStatus>();

    components.AddRange(Applications.Select(application =>
      new ComponentStatus(
        application.Name,
        application.Status == "Online" ? "ok" : "error")));

    components.AddRange(Storage.Select(storage =>
      new ComponentStatus(storage.Label, storage.Status)));

    components.AddRange(Services.Select(service =>
      new ComponentStatus(service.Label, service.Status)));

    if (Docker.Status != "ok" &&
      !Docker.Containers.Any(container => container.Status == "error"))
    {
      components.Add(new ComponentStatus(
        "Docker container checks", Docker.Status));
    }

    components.AddRange(Docker.Containers.Select(container =>
      new ComponentStatus(
        $"Container: {container.Name}",
        container.Status)));

    components.Add(new ComponentStatus("Backup", Backup.Status));

    Components = components.ToArray();
  }
}