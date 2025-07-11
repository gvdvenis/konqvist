namespace Konqvist.Web.Models.Actors;

public abstract class Actor : Marker
{
    protected Actor(Coordinate position) : this()
    {
        Coordinates = new Coordinates(position);
    }

    protected Actor()
    {
        Id = GetType().Name + "_" + Guid.NewGuid();
        Type = MarkerType.MarkerPin;
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