using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public record DailyQuote(string Content, string? Author);
public record QuoteResponse(DailyQuote? Proverb);

public record CurrentWeather(
    [property: JsonPropertyName("temperature_2m")] double? Temperature,
    [property: JsonPropertyName("weather_code")] int? Code);

public record WeatherResponse(CurrentWeather? Current);

public class HomeWidgets(
    IHttpClientFactory clients,
    IConfiguration configuration)
{
    private static readonly TimeZoneInfo Helsinki =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");

    private readonly SemaphoreSlim quoteLock = new(1, 1);
    private DateOnly quoteDate;
    private DailyQuote? cachedQuote;

    public async Task<DailyQuote?> GetQuoteAsync(CancellationToken cancellationToken)
    {
        await quoteLock.WaitAsync(cancellationToken);

        try
        {
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Helsinki);
            var today = DateOnly.FromDateTime(now.DateTime);

            if (quoteDate == today && cachedQuote is not null)
            {
                return cachedQuote;
            }

            var url = configuration["Widgets:QuoteUrl"]
                ?? "http://127.0.0.1:5008/api/random?category=Computers";

            var response = await ReadAsync<QuoteResponse>(url, cancellationToken);
            var quote = response?.Proverb;

            if (quote is null || string.IsNullOrWhiteSpace(quote.Content))
            {
                return null;
            }

            quoteDate = today;
            cachedQuote = quote;
            return quote;
        }
        finally
        {
            quoteLock.Release();
        }
    }

    public async Task<CurrentWeather?> GetWeatherAsync(
        CancellationToken cancellationToken)
    {
        const string url =
            "https://api.open-meteo.com/v1/forecast" +
            "?latitude=63.6749&longitude=22.7042" +
            "&current=temperature_2m,weather_code&timezone=Europe%2FHelsinki";

        var response = await ReadAsync<WeatherResponse>(url, cancellationToken);
        return response?.Current;
    }

    private async Task<T?> ReadAsync<T>(
        string url,
        CancellationToken cancellationToken) where T : class
    {
        using var client = clients.CreateClient("HomeWidgets");

        try
        {
            return await client.GetFromJsonAsync<T>(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}