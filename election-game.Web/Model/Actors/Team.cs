using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class Team : Actor
{
    public string Name { get; }

    public Team(Coordinate position, string name) : base(position)
    {
        Name = name;
        PinColor = PinColor.Red;
    }

    #region Overrides of Object

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

    #endregion
}