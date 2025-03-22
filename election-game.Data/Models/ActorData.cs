using OpenLayers.Blazor;

namespace election_game.Data.Models;

public abstract class ActorData(string name, string color)
{
    public string Name { get; set; } = name;
    public string Color { get; set; } = color;
    public Coordinate Location { get; set; }
}