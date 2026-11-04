using Microsoft.AspNetCore.Mvc.RazorPages;

public class StatusModel(
    IConfiguration configuration,
    ApplicationChecker checker) : PageModel
{
    public MonitoredApplication[] Applications { get; private set; } = [];

    public ApplicationStatus[] ApplicationStatuses { get; private set; } = [];

    public IEnumerable<MonitoredApplication> AffectedApplications =>
        Applications.Where(application => application.Status != "Online");

    public int UnavailableCount => AffectedApplications.Count();

    public bool AllHealthy => UnavailableCount == 0;

    public async Task OnGetAsync()
    {
        var definitions = configuration
            .GetSection("Applications")
            .Get<ApplicationDefinition[]>()
            ?? [];

        var checks = definitions.Select(application =>
            checker.CheckAsync(application.Name, application.HealthUrl));

        Applications = await Task.WhenAll(checks);

        ApplicationStatuses = definitions
            .Zip(Applications, (definition, check) =>
                new ApplicationStatus(definition, check))
            .ToArray();
    }
}