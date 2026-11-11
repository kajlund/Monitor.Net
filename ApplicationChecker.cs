using System.Diagnostics;

public class ApplicationChecker(HttpClient client)
{
  public async Task<MonitoredApplication> CheckAsync(
      string name,
      string healthUrl)
  {
    var stopwatch = Stopwatch.StartNew();
    try
    {
      using var response = await client.GetAsync(healthUrl);

      var status = response.IsSuccessStatusCode
          ? "Online"
          : "Error";

      return new MonitoredApplication(name, status, stopwatch.ElapsedMilliseconds);
    }
    catch (HttpRequestException exception)
    {
      Console.Error.WriteLine(
        $"Health check failed for {name} ({healthUrl}): {exception}");

      return new MonitoredApplication(
        name, "Unavailable", stopwatch.ElapsedMilliseconds);
    }
    catch (OperationCanceledException)
    {
      return new MonitoredApplication(name, "Timed out", stopwatch.ElapsedMilliseconds);
    }
  }
}