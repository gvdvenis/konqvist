using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public abstract class Actor : Marker
{
    protected Actor(Coordinate position)
    {
        Id = GetType().Name + "_" + Guid.NewGuid();
        Coordinates = new Coordinates(position);
        Type = MarkerType.MarkerPin;
        UpdateCoordinates();
    }

    public Task UpdateLocation(Coordinate position)
    {
        Coordinates = new Coordinates(position);
        return UpdateCoordinates();
    }

    #region Overrides of Object

    /// <inheritdoc />
    public override string ToString()
    {
        return Id;
    }

    #endregion
}