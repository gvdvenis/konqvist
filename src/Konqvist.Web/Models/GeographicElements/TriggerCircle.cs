namespace Konqvist.Web.Models.GeographicElements;

public class TriggerCircle : Circle
{
    public string DistrictName { get; }

    public TriggerCircle(DistrictData districtData): base(districtData.TriggerCircleCenter, 25)
    {
        DistrictName = districtData.Name;
        Styles = [MapStyles.DistrictTriggerStyle];
    }
}