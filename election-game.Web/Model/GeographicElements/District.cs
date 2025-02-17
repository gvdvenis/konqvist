using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class District : Region
{
    public District(Coordinates coordinates, Dictionary<string, dynamic> properties) : base(coordinates, properties)
    {
        if (!this.IsDistrict())
            throw new ArgumentException("The region is not a district.");

        Owner = Properties.GetValueOrDefault("owner")?.ToString() ?? "";
    }

    public string Owner { get; }
}