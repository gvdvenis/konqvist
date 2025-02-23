using System.Text.Json.Serialization;
using OpenLayers.Blazor;

namespace election_game.Data.Model.MapElements;

public class DistrictData
{
    [JsonPropertyName("coordinates")]
    [JsonConverter(typeof(CoordinateArrayConverter))]
    public required List<Coordinate> Coordinates { get; set; }
    public Coordinate TriggerCircleCenter { get; set; }
    public TeamData Owner { get; set; }
    public string Name { get; set; }
}

public class TeamData(string name)
{
    public string Name { get; } = name;
}

