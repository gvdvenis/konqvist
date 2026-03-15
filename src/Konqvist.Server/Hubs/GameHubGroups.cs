namespace Konqvist.Server.Hubs;

internal static class GameHubGroups
{
    public static string Game(int gameSessionId) => $"game:{gameSessionId}";

    public static string Team(int gameSessionId, int teamSessionId) => $"game:{gameSessionId}:team:{teamSessionId}";

    public static string Player(int playerSessionId) => $"player:{playerSessionId}";
}
