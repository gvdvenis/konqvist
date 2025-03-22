using election_game.Data.Models;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class District : Region
{
    public District(DistrictData districtData) : base(new Coordinates(districtData.Coordinates.ToList()))
    {
        Name = districtData.Name;
        Owner = Team.CreateFromDataOrDefault(districtData.Owner);

        TriggerCircle = new Circle(districtData.TriggerCircleCenter, 25)
        {
            Styles = [MapStyles.DistrictTriggerStyle]
        };
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
    public Circle TriggerCircle { get; }
    public Dictionary<string, int> Resources { get; }

    internal Task SetOwner(Team newOwner)
    {
        Owner ??= newOwner;
        Owner.TextColor = newOwner.TextColor;
        Styles = [MapStyles.DistrictOwnerStyle(Owner?.TextColor ?? "Transparent")];

        return Task.CompletedTask;
    }
}

