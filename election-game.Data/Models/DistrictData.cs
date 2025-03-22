using OpenLayers.Blazor;

namespace election_game.Data.Models;

public class DistrictData : IShapeData
{
    public required IEnumerable<Coordinate> Coordinates { get; set; }
    public Coordinate TriggerCircleCenter { get; set; }
    public TeamData? Owner { get; set; }
    public string Name { get; set; }
    public DistrictResourcesData Resources { get; set; }
}