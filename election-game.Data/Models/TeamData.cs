namespace election_game.Data.Models;

public class TeamData(string name, string color): ActorData(name, color)
{
    public string Description => $"Team {Name}";
}