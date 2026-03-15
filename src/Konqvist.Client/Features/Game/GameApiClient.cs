using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Konqvist.Client.Features.Game;

public sealed class GameApiClient(HttpClient httpClient)
{
    public async Task<StartGameResult> StartGameAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/game/start");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return StartGameResult.Success();
            }

            var error = await ReadErrorMessageAsync(response, cancellationToken);
            return new StartGameResult(false, error ?? "Could not start the game.");
        }
        catch (HttpRequestException)
        {
            return new StartGameResult(false, "Could not reach the server API.");
        }
        catch (TaskCanceledException)
        {
            return new StartGameResult(false, "Start game request timed out.");
        }
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync(
                GameJsonSerializerContext.Default.GameErrorResponse,
                cancellationToken);
            return body?.Message;
        }
        catch (NotSupportedException)
        {
            return $"{(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (System.Text.Json.JsonException)
        {
            return $"{(int)response.StatusCode} {response.ReasonPhrase}";
        }
    }
}
