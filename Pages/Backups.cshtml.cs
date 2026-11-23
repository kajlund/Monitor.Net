using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Monitor.Features.Backups;
using Monitor.Features.Storage;

namespace Monitor.Pages;

public class BackupsModel(
  BackupChecker backupChecker,
  StorageChecker storageChecker,
  BackupLogReader logReader,
  BackupExecutionReader executionReader,
  BackupRunner backupRunner,
  IOptions<BackupOptions> backupOptions) : PageModel
{
  public BackupExecution[] Executions { get; private set; } = [];

  public BackupLog? LatestLog { get; private set; }
  public BackupSnapshot[] Snapshots { get; private set; } = [];
  public Dictionary<string, BackupLog> ExecutionLogs { get; private set; } = [];
  public StorageStatus? BackupStorage { get; private set; }
  public BackupServiceState ServiceState { get; private set; }
  = BackupServiceState.Unknown;

  public async Task OnGetAsync()
  {
    ServiceState = await backupRunner.GetStateAsync();
    Snapshots = backupChecker.GetSnapshots();

    var storage = await storageChecker.CheckAsync();
    BackupStorage = storage.FirstOrDefault(
      item => item.Path == "/srv/backup");

    Executions = executionReader.GetExecutions();
    Executions = Executions.Take(backupOptions.Value.ExecutionHistoryLimit).ToArray();
    foreach (var execution in Executions)
    {
      var log = logReader.GetLog(execution.Name);

      if (log is not null)
      {
        ExecutionLogs[execution.Name] = log;
      }
    }

  }

  public async Task<IActionResult> OnPostRunAsync()
  {
    if (User.Identity?.IsAuthenticated != true ||
        !User.IsInRole("Administrator"))
    {
      return Challenge();
    }

    var state = await backupRunner.GetStateAsync();

    if (state != BackupServiceState.Idle)
    {
      TempData["BackupMessage"] = state switch
      {
        BackupServiceState.Running => "A backup is already running.",
        BackupServiceState.Failed => "Backup service is in a failed state.",
        _ => "Backup service state could not be verified."
      };

      return RedirectToPage();
    }

    var result = await backupRunner.StartAsync();

    TempData["BackupMessage"] = result.Message;

    return RedirectToPage();
  }
}