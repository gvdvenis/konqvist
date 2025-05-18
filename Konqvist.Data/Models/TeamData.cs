namespace Konqvist.Data.Models;

public class TeamData(string name, string color) : ActorData(name, color)
{
    public string Description => $"Team {Name}";
    public bool PlayerLoggedIn { get; set; }
    public bool IsDisabled { get; set; }
    public ResourcesData AdditionalResources { get; set; } = ResourcesData.Empty;
    public int BonusPointsFromVoting { get; set; } = 0; // Bonus points for voting for the winner
    public static TeamData Empty { get; } = new(string.Empty, "#05203b");
}

public enum TeamMemberRole
{
    TeamCaptain,
    Runner,
    Observer,
    GameMaster
}