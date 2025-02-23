using election_game.Data.Model.MapElements;

namespace ElectionGame.Web;

public class GameApiClient(HttpClient httpClient)
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

    public async Task<MapData> GetMapDataAsync(CancellationToken cancellationToken = default)
    {
        var map = await httpClient.GetFromJsonAsync<MapData>("/mapdata", cancellationToken);

        if (map is null)
        {
            throw new InvalidOperationException("Map data is null");
        }

        return map;
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
