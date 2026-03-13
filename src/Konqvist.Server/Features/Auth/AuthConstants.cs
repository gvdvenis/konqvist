namespace Konqvist.Server.Features.Auth;

public static class AuthConstants
{
    public const string AuthenticationScheme = "PlayerCookie";
    public const string CookieName = "__Host-konqvist.auth";

    public static class ClaimTypes
    {
        public const string PlayerSessionId = "konqvist:player_session_id";
    }
}
