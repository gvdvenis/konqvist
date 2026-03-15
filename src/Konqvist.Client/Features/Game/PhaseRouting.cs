using Konqvist.Client.Core.Models;

namespace Konqvist.Client.Features.Game;

public static class PhaseRouting
{
    public static string? ResolveTargetRoute(GamePhase phase, PlayerRole? role)
    {
        if (!role.HasValue || role == PlayerRole.GameMaster)
        {
            return null;
        }

        return phase switch
        {
            GamePhase.WaitingForPlayers => "/waiting",
            GamePhase.Gathering => "/map",
            GamePhase.Voting => "/vote",
            GamePhase.RoundResolution => "/vote",
            GamePhase.Finished => "/finished",
            _ => null
        };
    }

    public static bool IsPreStartPhase(GamePhase phase) => phase == GamePhase.WaitingForPlayers;

    public static bool ShouldForceWaitingRoute(
        bool isAuthenticated,
        bool isGameMaster,
        GamePhase currentPhase,
        string currentRoute)
    {
        if (!isAuthenticated || isGameMaster || !IsPreStartPhase(currentPhase))
        {
            return false;
        }

        var normalizedRoute = NormalizeRoute(currentRoute);
        return normalizedRoute is not "/waiting"
               && !normalizedRoute.EndsWith("/logout", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return "/";
        }

        var normalizedRoute = route.StartsWith("/", StringComparison.Ordinal) ? route : $"/{route}";
        normalizedRoute = normalizedRoute.TrimEnd('/');
        return string.IsNullOrEmpty(normalizedRoute) ? "/" : normalizedRoute;
    }
}
