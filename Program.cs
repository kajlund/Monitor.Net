using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;

using Monitor.Features.Applications;
using Monitor.Features.Authentication;
using Monitor.Features.Backups;
using Monitor.Features.Docker;
using Monitor.Features.Services;
using Monitor.Features.Storage;
using Monitor.Features.SystemVitals;
using Monitor.Features.Widgets;

var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsLinux())
{
  builder.Configuration.AddJsonFile(
    "/etc/dreamquest/monitor/auth.json",
    optional: false,
    reloadOnChange: false);
}

builder.Services.Configure<AdminAuthenticationOptions>(
  builder.Configuration.GetSection("AdminAuthentication"));

builder.Services.Configure<BackupOptions>(
  builder.Configuration.GetSection("Backups"));

builder.Services.Configure<StorageOptions>(
  builder.Configuration.GetSection("Storage"));

builder.Services.AddRazorPages();

var dataProtectionPath = OperatingSystem.IsLinux()
  ? "/var/lib/dreamquest/monitor/keys"
  : Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "DreamQuest",
      "Monitor",
      "keys");

Directory.CreateDirectory(dataProtectionPath);

builder.Services.AddDataProtection()
  .SetApplicationName("DreamQuest.Monitor")
  .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));

builder.Services
  .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
  .AddCookie(options =>
  {
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.Cookie.Name = "DreamQuest.Monitor.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
  });
builder.Services.AddAuthorization();

builder.Services.AddHttpClient<ApplicationChecker>(client =>
{
  client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<AdminAuthenticationService>();
builder.Services.AddSingleton<SystemVitalsReader>();
builder.Services.AddSingleton<StorageChecker>();
builder.Services.AddSingleton<ServiceChecker>();
builder.Services.AddSingleton<DockerChecker>();
builder.Services.AddSingleton<BackupChecker>();
builder.Services.AddSingleton<BackupRunner>();
builder.Services.AddSingleton<BackupLogReader>();
builder.Services.AddSingleton<BackupExecutionReader>();
builder.Services.AddHttpClient("HomeWidgets", client =>
{
  client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddSingleton<HomeWidgets>();

builder.Services.AddRateLimiter(options =>
{
  options.AddPolicy("login", context =>
    RateLimitPartition.GetFixedWindowLimiter(
      partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
      factory: _ => new FixedWindowRateLimiterOptions
      {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
      }));

  options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});


var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapRazorPages();

var applications = builder.Configuration.GetSection("Applications").Get<ApplicationDefinition[]>() ?? Array.Empty<ApplicationDefinition>();

app.MapGet("/api/applications", async (ApplicationChecker checker) =>
{
  var checks = applications.Select(application =>
    checker.CheckAsync(application.Name, application.HealthUrl));

  return await Task.WhenAll(checks);
});

app.Run();
