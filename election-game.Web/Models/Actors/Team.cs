using election_game.Data.Models;

namespace ElectionGame.Web.Model;

public class Team : Actor
{

    public static Team? CreateFromDataOrDefault(TeamData? teamData)
    {
        return teamData is null 
            ? null 
            : new Team(teamData);
    }

    public string Name { get; }

    public Team(string teamName)
    {
        Name = teamName;
        Fill = "white";
        Type = MarkerType.MarkerAwesome;
        Text = "\uf206";
        TextScale = 1.2;
    }

    public Team(TeamData teamData) : this(teamData.Name)
    {
        TextColor = teamData.Color;
        Coordinate = teamData.Location;
    }

    #region Overrides of Object

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

    #endregion
}