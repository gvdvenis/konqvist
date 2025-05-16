using System.Text.RegularExpressions;

namespace Konqvist.Web.Services;

public class GameModeRoutingService(
    SessionProvider sessionProvider,
    MapDataStore mapDataStore)
{

    public async Task<RoutingRule> GetRoutingRule(GameRole role, string page)
    {
        var rule = _routingRules.FirstOrDefault(rule => rule.Role == role && Regex.IsMatch(page, rule.PagePattern));

        if (rule is not null)
        {
            if (rule.RedirectOnMatch == "{GameState}")
                return rule with { RedirectOnMatch = await GetGameStateRoutePath() };

            return rule;
        }

        Console.WriteLine($"Warning: No routing rule defined for role '{role}' and page '{page}'. Redirecting to root path.");
        return new RoutingRule(role, ".*", false, false, "/");
    }

    /// <summary>
    ///     Validates whether the specified route is allowed based on the user's session and role.
    /// </summary>
    /// <remarks>
    ///     The validation considers the user's authentication status and role, as well as the routing
    ///     rules associated with the specified path. 
    /// </remarks>
    /// <param name="path">The requested route path to validate.</param>
    /// <returns>
    ///     The redirect page to navigate to if the route matches a specific route rule, or
    ///     <see langword="null"/> if no redirection is required.
    /// </returns>
    public async Task<string?> GetForcedRedirectRoute(string path)
    {
        var session = sessionProvider.Session;
        var role = session.GameRole;

        // Find the matching routing rule
        var rule = await GetRoutingRule(role, path);
        
        return rule.RedirectOnMatch;
    }

    public async Task<string?> GetGameStateRoutePath(RoundKind? appState = null)
    {
        appState ??= await mapDataStore.GetCurrentAppState();

        return appState switch
        {
            RoundKind.Voting => "voting",
            RoundKind.GameOver => "gameover",
            RoundKind.NotStarted => "waitforstart",
            RoundKind.GatherResources => "map",
            _ => null
        };
    }

    private readonly List<RoutingRule> _routingRules =
    [
        // Anonymous rules
        new (GameRole.Anonymous, "^login(/[^/]+)?$", false, true),

        // GameMaster rules
        new (GameRole.GameMaster, "^(login|\\s*)$", true, true, "management"),
        new (GameRole.GameMaster, "^(management|map|maptestenable|maptestdisable|voting|resetgame|logout)$", true, true),
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

public record RoutingRule(
    GameRole Role,
    string PagePattern,
    bool RequiresLogin,
    bool IsAllowed,
    string? RedirectOnMatch = null);
