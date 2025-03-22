using System.Text.Json.Serialization;
using OpenLayers.Blazor;

namespace election_game.Data.Models;

public interface IShapeData
{
    [JsonPropertyName("coordinates")]
    [JsonConverter(typeof(CoordinateArrayConverter))]
    public IEnumerable<Coordinate> Coordinates { get; set; }
}