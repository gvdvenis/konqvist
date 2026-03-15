namespace Konqvist.Server.Features.Auth;

public static class AuthConstants
{
    public const string AuthenticationScheme = "PlayerCookie";
    public const string CookieName = "__Host-konqvist.auth";

    public static class ClaimTypes
    {
        public const string PlayerSessionId = "konqvist:player_session_id";
        public const string TeamTemplateId = "konqvist:team_template_id";
        public const string GameSessionId = "konqvist:game_session_id";
    }
}
