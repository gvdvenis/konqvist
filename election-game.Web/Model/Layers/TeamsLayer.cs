using election_game.Data.Stores;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class TeamsLayer: Layer
{
    private readonly MapDataStore _mapDataStore;

    public TeamsLayer(MapDataStore mapDataStore)
    {
        _mapDataStore = mapDataStore;
        Id = nameof(TeamsLayer);
        LayerType = LayerType.Vector;
        SelectionEnabled = false;
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await InitLayer();
    }

    private async Task InitLayer()
    {
        var teamData = await _mapDataStore.GetTeams(true);
        var teams = teamData.Select(td => new Team(td));
        ShapesList.AddRange(teams);
    }

    #endregion

    public async Task UpdateTeamPosition(string sessionTeamName, Coordinate coordinate)
    {
        await _mapDataStore.UpdateTeamPosition(sessionTeamName, coordinate);
        var localTeam = ShapesList.OfType<Team>().FirstOrDefault(t => t.Name == sessionTeamName);
        localTeam?.UpdateLocation(coordinate);
    }
}
