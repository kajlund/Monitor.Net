var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient<ApplicationChecker>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

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
