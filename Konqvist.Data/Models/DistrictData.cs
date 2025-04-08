using OpenLayers.Blazor;

namespace Konqvist.Data.Models;

public class DistrictData : IShapeData
{
    public required IEnumerable<Coordinate> Coordinates { get; set; }
    public Coordinate TriggerCircleCenter { get; set; }
    public TeamData? Owner { get; set; }
    public string Name { get; set; } = string.Empty;
    public ResourcesData Resources { get; set; } = ResourcesData.Empty;
    public bool IsClaimable { get; set; } = true;
}