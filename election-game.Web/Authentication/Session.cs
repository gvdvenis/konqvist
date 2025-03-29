using System.Security.Claims;

namespace ElectionGame.Web.Authentication;

public record Session
{
    public GameRole GameRole { get; private init; }
    public string UserName { get; private init; } = string.Empty;
    public string TeamName { get; private init; } = string.Empty;

    public bool IsAuthenticated { get; private init; }
    public bool IsAdmin => GameRole == GameRole.GameMaster;
    public bool IsPlayer => GameRole == GameRole.Runner;
    public bool IsTeamLeader => GameRole == GameRole.TeamLeader;

    public static Session Empty { get; } = new();

    public static async ValueTask<Session> CreateFromAuthenticationState(Task<AuthenticationState>? authStateTask)
    {
        if (authStateTask is null) return Empty;

        var authState = await authStateTask;
        return CreateWithAuthState(authState);
    }

    public static Session CreateWithAuthState(AuthenticationState authState)
    {
        var user = authState.User;
        
        return new Session
        {
            GameRole = Enum.Parse<GameRole>(user.FindFirst(ClaimTypes.Role)?.Value ?? nameof(GameRole.Anonymous)),
            UserName = user.Identity?.Name ?? string.Empty,
            IsAuthenticated = user.Identity?.IsAuthenticated ?? false,
            TeamName = user.FindFirst(ClaimTypes.UserData)?.Value ?? string.Empty
        };
    }
}