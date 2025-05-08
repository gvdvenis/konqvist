using System.Text.RegularExpressions;

namespace Konqvist.Web.Services;

public class GameModeRoutingService(
    NavigationManager navigationManager,
    SessionProvider sessionProvider,
    MapDataStore mapDataStore)
{
    public async Task TryNavigateToGameMode()
    {
        if (sessionProvider.Session.IsAdmin) return;
        var appState = await mapDataStore.GetCurrentAppState();

        switch (appState)
        {
            case RoundKind.Voting:
                TryNavigate("voting");
                break;
            case RoundKind.GameOver:
                TryNavigate("gameover");
                break;
            case RoundKind.NotStarted:
                TryNavigate("waitforstart");
                break;
            case RoundKind.GatherResources:
                TryNavigate("map");
                break;
            default:
                if (sessionProvider.Session.IsAdmin) TryNavigate("management");
                break;
        }
    }

    public RoutingRule GetRoutingRule(GameRole role, string page)
    {
        var rule = _routingRules.FirstOrDefault(rule => rule.Role == role && Regex.IsMatch(page, rule.PagePattern));

        if (rule is not null) return rule;

        Console.WriteLine($"Warning: No routing rule defined for role '{role}' and page '{page}'. Redirecting to root path.");
        return new RoutingRule(role, ".*", false, false, "/");
    }

    public async Task<string> DetermineGameStateRedirect()
    {
        var appState = await mapDataStore.GetCurrentAppState();

        return appState switch
        {
            RoundKind.Voting => "voting",
            RoundKind.GameOver => "gameover",
            RoundKind.NotStarted => "waitforstart",
            RoundKind.GatherResources => "map",
            _ => "management"
        };
    }

    public async Task<(bool IsAllowed, string? RedirectPage)> ValidateRoute(string path)
    {
        var session = sessionProvider.Session;
        var role = session.GameRole;
        bool isAuthenticated = session.IsAuthenticated;

        // Find the matching routing rule
        var rule = GetRoutingRule(role, path);

        if (rule.IsAllowed && (!rule.RequiresLogin || isAuthenticated))
            return (true, rule.RedirectOnMatch);

        // Determine the redirect page
        if (rule.RedirectOnMatch != "{GameState}")
            return (rule.IsAllowed, rule.RedirectOnMatch);

        // Use TryNavigateToGameMode logic to determine the actual redirect page
        string redirectPage = await DetermineGameStateRedirect();
        return (rule.IsAllowed, redirectPage);
    }

    private void TryNavigate(string page)
    {
        if (navigationManager.Uri.Contains(page))
            return;

        navigationManager.NavigateTo($"/{page}");
    }

    private readonly List<RoutingRule> _routingRules =
    [
        // Anonymous rules
        new (GameRole.Anonymous, "^login(/[^/]+)?$", false, true),

        // GameMaster rules
        new (GameRole.GameMaster, "^(login|\\s*)$", true, true, "management"),
        new (GameRole.GameMaster, "^(management|map|voting|resetgame|logout)$", true, true),
        new (GameRole.GameMaster, "^(waitforstart|gameover)$", true, false, "management"),

        // TeamLeader rules
        new (GameRole.TeamLeader, "^(login|map|voting|\\s*)$", true, true, "{GameState}"),
        new (GameRole.TeamLeader, "^(management|logout)$", true, true),
        new (GameRole.TeamLeader, "^(resetgame|waitforstart|gameover)$", true, false, "{GameState}"),

        // Runner rules
        new (GameRole.Runner, "^(login|map|voting|\\s*)$", true, true, "{GameState}"),
        new (GameRole.Runner, "^(logout)$", true, true),
        new (GameRole.Runner, "^(management|resetgame|waitforstart|gameover)$", true, false, "{GameState}")
    ];
}

public record RoutingRule(GameRole Role, string PagePattern, bool RequiresLogin, bool IsAllowed, string? RedirectOnMatch = null);
