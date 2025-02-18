namespace ElectionGame.Web;

public class WeatherApiClient(HttpClient httpClient)
{
    public async Task<IQueryable<WeatherForecast>?> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<WeatherForecast>? forecasts = null;

        await foreach (var forecast in httpClient.GetFromJsonAsAsyncEnumerable<WeatherForecast>("/weatherforecast", cancellationToken))
        {
            if (forecasts?.Count >= maxItems)
            {
                break;
            }

            if (forecast is null) continue;
            forecasts ??= [];
            forecasts.Add(forecast);
        }

        return forecasts?.AsQueryable();
    }

    public async Task<string> GetMapDataAsync(CancellationToken cancellationToken = default)
    {
       return await httpClient.GetStringAsync("/mapdata", cancellationToken);
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
