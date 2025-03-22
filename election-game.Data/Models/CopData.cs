namespace election_game.Data.Models;

public class CopData(string name, string color = "blue") : ActorData(name, color)
{
    public string Description => $"Cop {Name}";
}