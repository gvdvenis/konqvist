using election_game.Data.Model.MapElements;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class District : Region
{
    public District(DistrictData districtData) : base(new Coordinates(districtData.Coordinates.ToList()))
    {
        Name = districtData.Name;
        Owner = new Team(districtData.Owner);

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

        Styles = [MapStyles.DistrictOwnerStyle(Owner.Name)];
    }


    public async Task ShowPopup()
    {
        if (Map is null) return;
        await Map.ShowPopup(this, TriggerCircle.Center);
    }

    public string Name { get; }
    public Team Owner { get; }
    public Circle TriggerCircle { get; }
    public Dictionary<string, int> Resources { get; }
}

