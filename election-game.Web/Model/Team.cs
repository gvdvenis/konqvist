using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class Cop : Marker
{
    public Cop(Coordinate position)
    {
        Id = Guid.NewGuid().ToString();
        Type = MarkerType.MarkerAwesome;
        Coordinates = new Coordinates(position);
        PinColor = PinColor.Red;
        UpdateCoordinates();
    }

    public void UpdateLocation(Coordinate position)
    {
        Coordinates = new Coordinates(position);
        UpdateCoordinates();
    }
}

public class Team : Marker
{
    public string Name { get; }

    public Team(Coordinate position, string name)
    {
        Id = Guid.NewGuid().ToString();

        Name = name;
        Type = MarkerType.MarkerPin;
        Coordinates = new Coordinates(position);
        PinColor = PinColor.Blue;
        UpdateCoordinates();
    }

    public void UpdateLocation(Coordinate position)
    {
       Coordinates = new Coordinates(position);
       UpdateCoordinates();
    }

    #region Overrides of Object

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

    #endregion
}