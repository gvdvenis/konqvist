using Microsoft.FluentUI.AspNetCore.Components;

namespace Konqvist.Web.Models.GeographicElements;

public class District : Region
{
    public District(DistrictData districtData) : base(districtData.Coordinates)
    {
        Name = districtData.Name;
        Owner = Team.CreateFromDataOrEmtpy(districtData.Owner);
        IsClaimable = districtData.IsClaimable;
        TriggerCircle = new TriggerCircle(districtData);
        Resources = new Resources(districtData.Resources);
        ResourceDictionary = Resources.ToDictionary();
        Styles = [MapStyles.DistrictOwnerStyle(Owner.TextColor)];
    }
    
    public string Name { get; }
    public Team Owner { get; }
    public TriggerCircle TriggerCircle { get; }

    public bool IsClaimable { get; set; }
    public Resources Resources { get; set; }
    public Dictionary<string, (int Amount, Icon Icon)> ResourceDictionary { get; }

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

