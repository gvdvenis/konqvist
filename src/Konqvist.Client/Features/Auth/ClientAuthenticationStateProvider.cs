using System.Security.Claims;
using Konqvist.Client.Features.Login;
using Microsoft.AspNetCore.Components.Authorization;

namespace Konqvist.Client.Features.Auth;

public sealed class ClientAuthenticationStateProvider(LoginApiClient loginApiClient) : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public static class ClaimTypes
    {
        public const string Team = "konqvist:team";
    }

    public string? LastKnownTeamName { get; private set; }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = await loginApiClient.GetCurrentIdentityAsync();
        if (identity is null)
        {
            return AnonymousState;
        }

        LastKnownTeamName = identity.Team;
        var claims = new[]
        {
            new Claim(System.Security.Claims.ClaimTypes.Role, identity.Role),
            new Claim(ClaimTypes.Team, identity.Team),
            new Claim("konqvist:player_session_id", identity.PlayerSessionId.ToString())
        };

        return new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "PlayerCookie")));
    }

    public void RefreshAuthenticationState()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
