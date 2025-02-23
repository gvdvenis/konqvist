using election_game.Data.Model.MapElements;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class Team : Actor
{
    public string Name { get; }

    public Team(string teamName)
    {
        Name = teamName;
        PinColor = PinColor.Red;
    }

    public Team(TeamData teamData) : this(teamData.Name) { }

    #region Overrides of Object

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

    #endregion
}