namespace Konqvist.Web.Models.GeographicElements;

public class District : Region
{
    public District(DistrictData districtData) : base(districtData.Coordinates)
    {
        Name = districtData.Name;
        Owner = Team.CreateFromDataOrDefault(districtData.Owner);
        IsClaimable = districtData.IsClaimable;
        
        TriggerCircle = new TriggerCircle(districtData);
        Resources = new Resources(districtData.Resources);
        ResourceDictionary = Resources.ToDictionary();
        Styles = [MapStyles.DistrictOwnerStyle(Owner?.TextColor ?? "Transparent")];
    }

    public bool IsClaimable { get; set; }

    public async Task ShowPopup()
    {
        if (Map is null) return;
        await Map.ShowPopup(this, TriggerCircle.Center);
    }

    public string Name { get; }
    public Team? Owner { get; private set; }
    public TriggerCircle TriggerCircle { get; }
    public Resources Resources { get; set; }
    public Dictionary<string, int> ResourceDictionary { get; }

    /// <summary>
    ///     Check if the given location is within the trigger circle of this district
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    public bool IsAtLocation(Coordinate location)
    {
        return location.DistanceTo(TriggerCircle.Center) * 1000 < TriggerCircle.Radius;
    }
}

