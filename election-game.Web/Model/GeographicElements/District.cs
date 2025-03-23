using election_game.Data.Models;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class District : Region
{
    public District(DistrictData districtData) : base(districtData.Coordinates)
    {
        Name = districtData.Name;
        Owner = Team.CreateFromDataOrDefault(districtData.Owner);

        TriggerCircle = new TriggerCircle(districtData.TriggerCircleCenter);
        Resources = new Dictionary<string, int>
        {
            {"Gold", districtData.Resources.R1},
            {"Votes", districtData.Resources.R2},
            {"People", districtData.Resources.R3},
            {"Oil", districtData.Resources.R4}
        };

        Styles = [MapStyles.DistrictOwnerStyle(Owner?.TextColor ?? "Transparent")];
    }
    
    public async Task ShowPopup()
    {
        if (Map is null) return;
        await Map.ShowPopup(this, TriggerCircle.Center);
    }

    public string Name { get; }
    public Team? Owner { get; private set; }
    public TriggerCircle TriggerCircle { get; }
    public Dictionary<string, int> Resources { get; }

    internal async Task SetOwner(Team newOwner)
    {
        Owner = newOwner;
        Owner.TextColor = newOwner.TextColor;
        Styles = [MapStyles.DistrictOwnerStyle(Owner?.TextColor ?? "Transparent")];
        await UpdateShape();
    }

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

