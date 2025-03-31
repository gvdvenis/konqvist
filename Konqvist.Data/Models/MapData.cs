using OpenLayers.Blazor;

namespace Konqvist.Data.Models;

public class MapData: IShapeData
{
    public required IEnumerable<Coordinate> Coordinates { get; set; } = [];

    public List<DistrictData> Districts { get; set; } = [];

    public static MapData Empty { get; } = new()
    {
        Coordinates = []
    };
}
