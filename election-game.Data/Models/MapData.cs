using OpenLayers.Blazor;

namespace election_game.Data.Models;

public class MapData: IShapeData
{
    public required IEnumerable<Coordinate> Coordinates { get; set; } = [];

    public List<DistrictData> Districts { get; set; } = [];

    public static MapData Empty { get; } = new()
    {
        Coordinates = []
    };
}
