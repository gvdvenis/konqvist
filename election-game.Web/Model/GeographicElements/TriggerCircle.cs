using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class TriggerCircle : Circle
{
    public TriggerCircle(Coordinate center) : base(center, 25)
    {
        Styles = [MapStyles.DistrictTriggerStyle];
    }
}