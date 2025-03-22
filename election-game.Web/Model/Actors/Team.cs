using System.Xml;
using election_game.Data.Models;
using Microsoft.FluentUI.AspNetCore.Components;
using OpenLayers.Blazor;

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
        Type = MarkerType.MarkerAwesome;
        Text = "\uf007";
    }

    public Team(TeamData teamData) : this(teamData.Name)
    {
        TextColor = teamData.Color;
    }

    #region Overrides of Object

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

    #endregion
}