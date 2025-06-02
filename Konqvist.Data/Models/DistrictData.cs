using OpenLayers.Blazor;

namespace Konqvist.Data.Models;

public class DistrictData : IShapeData
{
    // Static data
    public IEnumerable<Coordinate> Coordinates { get; init; } = [];

    // Todo: because this is set by the KML parser, it cannot not be init-only yet
    public Coordinate TriggerCircleCenter { get; set; } 

    public string Name { get; init; } = string.Empty;
    public ResourcesData Resources { get; init; } = ResourcesData.Empty;

    // properties that chang state during the game
    public bool IsClaimable { get; private set; } = true;
    public TeamData? Owner { get; private set; }

    internal void AssignDistrictOwner(TeamData team)
    {
        Owner = team;
        IsClaimable = false;
    }

    public void ReleaseClaim()
    {
        IsClaimable = true;
    }

    [Obsolete("this method should be removed. the caller should pass this as part of initialization instead")]
    internal void SetTriggerCircleCenter(Coordinate parseCoordinate)
    {
        TriggerCircleCenter = parseCoordinate;
    }
}