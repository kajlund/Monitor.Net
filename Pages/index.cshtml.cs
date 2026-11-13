using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitor.Features.Applications;
using Monitor.Features.Widgets;

public class IndexModel(
    IConfiguration configuration,
    HomeWidgets widgets) : PageModel
{
  public ApplicationDefinition[] Applications { get; private set; } = [];
  public DailyQuote? Quote { get; private set; }
  public CurrentWeather? Weather { get; private set; }

  public (string Description, string Icon) WeatherCondition => Weather?.Code switch
  {
    0 => ("Clear sky", "ph-sun"),
    1 => ("Mainly clear", "ph-sun"),
    2 => ("Partly cloudy", "ph-cloud-sun"),
    3 => ("Overcast", "ph-cloud"),
    45 or 48 => ("Fog", "ph-cloud-fog"),
    51 or 53 or 55 => ("Drizzle", "ph-cloud-rain"),
    56 or 57 or 66 or 67 => ("Freezing precipitation", "ph-cloud-rain"),
    61 or 63 or 65 => ("Rain", "ph-cloud-rain"),
    71 or 73 or 75 or 77 => ("Snow", "ph-snowflake"),
    80 or 81 or 82 => ("Rain showers", "ph-cloud-rain"),
    85 or 86 => ("Snow showers", "ph-snowflake"),
    95 or 96 or 99 => ("Thunderstorm", "ph-lightning"),
    _ => ("Conditions unavailable", "ph-cloud")
  };

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Applications = configuration
      .GetSection("Applications")
      .Get<ApplicationDefinition[]>()
      ?? [];

    var quoteTask = widgets.GetQuoteAsync(cancellationToken);
    var weatherTask = widgets.GetWeatherAsync(cancellationToken);

    await Task.WhenAll(quoteTask, weatherTask);

    Quote = await quoteTask;
    Weather = await weatherTask;
  }
}