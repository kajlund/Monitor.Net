using System.Globalization;

public class SystemVitalsReader
{
  public SystemVitals Read()
  {
    var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
    var load = ReadLoad();


    return new SystemVitals(
        Environment.MachineName,
        $"{uptime.Days}d {uptime.Hours}h",
        ReadTemperature(),
        ReadMemory(),
        load.Value,
        load.Detail
        );
  }

  private static (string Value, string Detail) ReadLoad()
  {
    if (!OperatingSystem.IsLinux())
    {
      return ("n/a", "");
    }

    try
    {
      var raw = File.ReadAllText("/proc/loadavg");

      var parts = raw.Split(
          (char[]?)null,
          StringSplitOptions.RemoveEmptyEntries);

      if (parts.Length == 0 ||
          !double.TryParse(
              parts[0],
              NumberStyles.Float,
              CultureInfo.InvariantCulture,
              out var load) ||
          !double.IsFinite(load) ||
          load < 0)
      {
        return ("n/a", "");
      }

      var percent = load / Environment.ProcessorCount * 100;

      return ($"{percent:F0}%", $"1 min · {load:F2}");
    }
    catch (IOException)
    {
      return ("n/a", "");
    }
    catch (UnauthorizedAccessException)
    {
      return ("n/a", "");
    }
  }

  private static string ReadTemperature()
  {
    if (!OperatingSystem.IsLinux())
    {
      return "n/a";
    }

    try
    {
      var raw = File.ReadAllText(
          "/sys/class/thermal/thermal_zone0/temp");

      if (!double.TryParse(
          raw.Trim(),
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out var millidegrees))
      {
        return "n/a";
      }

      return $"{millidegrees / 1000:F1} °C";
    }
    catch (IOException)
    {
      return "n/a";
    }
    catch (UnauthorizedAccessException)
    {
      return "n/a";
    }
  }

  private static string ReadMemory()
  {
    if (!OperatingSystem.IsLinux())
    {
      return "n/a";
    }

    try
    {
      var values = new Dictionary<string, long>();

      foreach (var line in File.ReadLines("/proc/meminfo"))
      {
        var parts = line.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2 &&
            long.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
          values[parts[0].TrimEnd(':')] = value;
        }
      }

      if (!values.TryGetValue("MemTotal", out var total) ||
          !values.TryGetValue("MemAvailable", out var available) ||
          total <= 0 ||
          available < 0 ||
          available > total)
      {
        return "n/a";
      }

      var usedGiB = (total - available) / (1024.0 * 1024);
      var totalGiB = total / (1024.0 * 1024);

      return $"{usedGiB:F1} / {totalGiB:F1} GiB";
    }
    catch (IOException)
    {
      return "n/a";
    }
    catch (UnauthorizedAccessException)
    {
      return "n/a";
    }
  }
}