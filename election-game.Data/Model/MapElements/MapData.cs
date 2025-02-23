using System.Text.Json.Serialization;
using OpenLayers.Blazor;

namespace election_game.Data.Model.MapElements;

public class MapData
{
    [JsonPropertyName("coordinates"), JsonConverter(typeof(CoordinateArrayConverter))]
    public List<Coordinate> Coordinates { get; set; } 

    public List<DistrictData> Districts { get; set; }
}
