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
        public const string GameStatus = "konqvist:game_status";
        public const string GamePhase = "konqvist:game_phase";
    }

    public string? LastKnownTeamName { get; private set; }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = await loginApiClient.GetCurrentIdentityAsync();
        if (identity is null)
        {
            return AnonymousState;
        }

        var role = string.IsNullOrWhiteSpace(identity.Role) ? "Runner" : identity.Role;
        var team = string.IsNullOrWhiteSpace(identity.Team) ? "No Team" : identity.Team;
        var gameStatus = string.IsNullOrWhiteSpace(identity.GameStatus) ? "Pending" : identity.GameStatus;
        var gamePhase = string.IsNullOrWhiteSpace(identity.GamePhase) ? "WaitingForPlayers" : identity.GamePhase;

        LastKnownTeamName = team;
        var claims = new[]
        {
            new Claim(System.Security.Claims.ClaimTypes.Role, role),
            new Claim(ClaimTypes.Team, team),
            new Claim("konqvist:player_session_id", identity.PlayerSessionId.ToString()),
            new Claim(ClaimTypes.GameStatus, gameStatus),
            new Claim(ClaimTypes.GamePhase, gamePhase)
        };

        return new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "PlayerCookie")));
    }

    public void RefreshAuthenticationState()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
