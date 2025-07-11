namespace Konqvist.Web.Models.GeographicElements;

public abstract class Region(IEnumerable<Coordinate> boundary) : Polygon([.. boundary])
{
    #region Overrides of Object

    public override string ToString()
    {
        return Id;
    }

    #endregion
}