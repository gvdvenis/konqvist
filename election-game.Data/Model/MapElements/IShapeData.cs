using OpenLayers.Blazor;
using System.Text.Json.Serialization;

namespace election_game.Data.Model.MapElements;

public interface IShapeData
{
    [JsonPropertyName("coordinates")]
    [JsonConverter(typeof(CoordinateArrayConverter))]
    public IEnumerable<Coordinate> Coordinates { get; set; }
}