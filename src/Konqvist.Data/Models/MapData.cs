using OpenLayers.Blazor;

namespace Konqvist.Data.Models;

public record MapData: IShapeData
{
    public required IEnumerable<Coordinate> Coordinates { get; set; } = [];

    public List<DistrictData> Districts { get; init; } = [];

    public static MapData Empty { get; } = new()
    {
        Coordinates = []
    };

    public ResourcesData GetResourcesForTeam(string teamName)
    {
        return Districts
            .Where(d => d.Owner is not null && d.Owner.Name == teamName)
            .Select(d => d.Resources)
            .Aggregate(ResourcesData.Empty, (acc, r) => acc + r);
    }
}
