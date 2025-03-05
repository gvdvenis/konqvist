namespace ElectionGame.Web.State;

public record Session
{
    public Role Role { get; init; }
    public string? UserName { get; init; }
    public string? TeamName { get; init; }
    public string? SessionId { get; init; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(SessionId);
    public bool IsAdmin => Role == Role.Admin;
    public bool IsPlayer => Role == Role.Player;
    public bool IsTeamLeader => Role == Role.TeamLeader;
}

public enum Role
{
    Admin,
    Player,
    TeamLeader,
}