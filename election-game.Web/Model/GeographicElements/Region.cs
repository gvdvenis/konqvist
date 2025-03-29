namespace ElectionGame.Web.Model;

public abstract class Region(IEnumerable<Coordinate> boundary) : Polygon(boundary.ToList())
{
    #region Overrides of Object

    public override string ToString()
    {
        return Id;
    }

    #endregion
}