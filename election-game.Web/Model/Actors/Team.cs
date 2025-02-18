using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class Team : Actor
{
    public string Name { get; }

    public Team(string name)
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