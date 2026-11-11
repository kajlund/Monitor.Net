var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient<ApplicationChecker>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<SystemVitalsReader>();
builder.Services.AddSingleton<StorageChecker>();
builder.Services.AddSingleton<ServiceChecker>();
builder.Services.AddSingleton<DockerChecker>();
builder.Services.AddSingleton<BackupChecker>();

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();

var applications = builder.Configuration.GetSection("Applications").Get<ApplicationDefinition[]>() ?? Array.Empty<ApplicationDefinition>();

app.MapGet("/api/applications", async (ApplicationChecker checker) =>
{
    var checks = applications.Select(application =>
        checker.CheckAsync(application.Name, application.HealthUrl));

    return await Task.WhenAll(checks);
});

app.Run();
