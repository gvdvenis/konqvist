using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public abstract class Region(Coordinates coordinates) : Polygon(coordinates[0])
{
    #region Overrides of Object

    public override string ToString()
    {
        return Id;
    }

    #endregion
}