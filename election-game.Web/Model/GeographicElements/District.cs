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
        Styles = [MapStyles.DistrictOwnerStyle(Owner.Name)];
    }

    public string Name { get; }
    public Team Owner { get; }
    public Circle TriggerCircle { get; }
}
