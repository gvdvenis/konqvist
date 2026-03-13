using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Konqvist.Client.Features.Login;

public sealed class LoginApiClient(HttpClient httpClient)
{
    public async Task<LoginResult> LoginAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(token), LoginJsonSerializerContext.Default.LoginRequest)
            };
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return LoginResult.Success();
            }

            var error = await ReadErrorMessageAsync(response, cancellationToken);
            return new LoginResult(false, error ?? "Login failed.");
        }
        catch (HttpRequestException)
        {
            return new LoginResult(false, "Could not reach the server API.");
        }
        catch (TaskCanceledException)
        {
            return new LoginResult(false, "Login request timed out.");
        }
    }

    public async Task<TeamStatusResult> GetTeamStatusAsync(string teamName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/team-status/{Uri.EscapeDataString(teamName)}");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var status = await response.Content.ReadFromJsonAsync(
                    LoginJsonSerializerContext.Default.TeamStatusResponse,
                    cancellationToken);
                return status is null
                    ? new TeamStatusResult(null, "Team status response was empty.")
                    : new TeamStatusResult(status, null);
            }

            var error = await ReadErrorMessageAsync(response, cancellationToken);
            return new TeamStatusResult(null, error ?? "Could not load team status.");
        }
        catch (HttpRequestException)
        {
            return new TeamStatusResult(null, "Could not reach the server API.");
        }
        catch (TaskCanceledException)
        {
            return new TeamStatusResult(null, "Team status request timed out.");
        }
    }

    public async Task<AuthIdentityResponse?> GetCurrentIdentityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync(
                LoginJsonSerializerContext.Default.AuthIdentityResponse,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<LogoutResult> LogoutAsync(string? teamName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(teamName)
                ? "/api/auth/logout"
                : $"/api/auth/logout?teamName={Uri.EscapeDataString(teamName)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return LogoutResult.Success();
            }

            var error = await ReadErrorMessageAsync(response, cancellationToken);
            return new LogoutResult(false, error ?? "Logout failed.");
        }
        catch (HttpRequestException)
        {
            return new LogoutResult(false, "Could not reach the server API.");
        }
        catch (TaskCanceledException)
        {
            return new LogoutResult(false, "Logout request timed out.");
        }
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync(
                LoginJsonSerializerContext.Default.AuthErrorResponse,
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
