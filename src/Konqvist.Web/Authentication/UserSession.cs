using System.Security.Claims;

namespace Konqvist.Web.Authentication;

public record UserSession
{
    public GameRole GameRole { get; private init; }
    public string UserName { get; private init; } = string.Empty;
    public string TeamName { get; private init; } = string.Empty;

    public bool IsAuthenticated { get; private init; }
    public bool IsAdmin => GameRole == GameRole.GameMaster;
    public bool IsPlayer => GameRole == GameRole.Runner;
    public bool IsTeamLeader => GameRole == GameRole.TeamLeader;

    public static UserSession Empty { get; } = new()
    {
        TeamName = "Unknown",
        GameRole = GameRole.Anonymous,
        IsAuthenticated = false,
        UserName = string.Empty
    };

    public static async ValueTask<UserSession> CreateFromAuthenticationState(Task<AuthenticationState>? authStateTask)
    {
        if (authStateTask is null) return Empty;

        var authState = await authStateTask;
        return CreateWithAuthState(authState);
    }

    public static UserSession CreateWithAuthState(AuthenticationState authState)
    {
        var user = authState.User;
        
        return new UserSession
        {
            GameRole = Enum.Parse<GameRole>(user.FindFirst(ClaimTypes.Role)?.Value ?? nameof(GameRole.Anonymous)),
            UserName = user.Identity?.Name ?? string.Empty,
            IsAuthenticated = user.Identity?.IsAuthenticated ?? false,
            TeamName = user.FindFirst(ClaimTypes.UserData)?.Value ?? string.Empty
        };
    }

    public static UserSession CreateFromUser(User loginUser)
    {
        return new UserSession
        {
            GameRole = loginUser.GameRole,
            UserName = loginUser.Name,
            IsAuthenticated = true,
            TeamName = loginUser.TeamName
        };
    }
}