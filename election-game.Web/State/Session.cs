using System.Security.Claims;

namespace ElectionGame.Web.State;

public record Session
{
    public Role Role { get; private init; }
    public string UserName { get; private init; } = string.Empty;
    public string TeamName { get; private init; } = string.Empty;

    public bool IsAuthenticated { get; private init; }
    public bool IsAdmin => Role == Role.Admin;
    public bool IsPlayer => Role == Role.Player;
    public bool IsTeamLeader => Role == Role.TeamLeader;

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
            Role = Enum.Parse<Role>(user.FindFirst(ClaimTypes.Role)?.Value ?? nameof(Role.Anonymous)),
            UserName = user.Identity?.Name ?? string.Empty,
            IsAuthenticated = user.Identity?.IsAuthenticated ?? false,
            TeamName = user.FindFirst(ClaimTypes.UserData)?.Value ?? string.Empty
        };
    }
}