using System.Text.Json.Serialization;
using OpenLayers.Blazor;

namespace election_game.Data.Model.MapElements;

public class MapData: IShapeData
{
    public required IEnumerable<Coordinate> Coordinates { get; set; } 

    public List<DistrictData> Districts { get; set; }
}
