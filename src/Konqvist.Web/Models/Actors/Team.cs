namespace Konqvist.Web.Models.Actors;

public class Team : Actor
{
    public static Team Empty { get; } = new("-");

    public static Team CreateFromDataOrEmtpy(TeamData? teamData)
    {
        return teamData is null
            ? Empty
            : new Team(teamData);
    }

    public string Name { get; }

    public bool RunnerLoggedIn { get; }

    public Team(string teamName)
    {
        Name = teamName;
        Fill = "white";
        Type = MarkerType.MarkerAwesome;
        Text = "\uf206";
        TextScale = 1.2;
        RunnerLoggedIn = false;
    }

    public Team(TeamData teamData) : this(teamData.Name)
    {
        TextColor = teamData.Color;
        Coordinate = teamData.Location;
        RunnerLoggedIn = teamData.PlayerLoggedIn;
    }

    //public new string TextColor { get; set; } = "Transparent";

    #region Overrides of Object

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

    #endregion
}