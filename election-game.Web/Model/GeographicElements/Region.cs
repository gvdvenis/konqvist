using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public abstract class Region : Polygon
{
    protected Region(Coordinates coordinates, Dictionary<string, dynamic> properties) : base(coordinates[0])
    {
        foreach ((string? key, dynamic? value) in properties) Properties.TryAdd(key, value);
    }

    public string Name => Properties.GetValueOrDefault("key")?.ToString() ?? Id;

    public string RegionType => Properties.GetValueOrDefault("region-type")?.ToString() ?? "";

    #region Overrides of Object

    public override string ToString()
    {
        return Name;
    }

    #endregion
}