using System.ComponentModel.Design;
using System.Diagnostics;
using election_game.Data.Models;
using election_game.Data.Stores;
using ElectionGame.Web.SignalR;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class GameMap : OpenStreetMap
{
    private readonly IGameHubClient _gameHubClient;
    private readonly MapDataStore _dataStore;
    private DistrictsLayer DistrictsLayer { get; set; } = new();
    private MapLayer MapLayer { get; set; } = new();
    private CopsLayer CopsLayer { get; set; } = new();

    public GameMap(IGameHubClient gameHubClient, MapDataStore dataStore)
    {
        _gameHubClient = gameHubClient;
        _dataStore = dataStore;
        Center = new Coordinate([6.261195479378347, 51.87638698662113]);
        Zoom = 16;
        MinZoom = 14;
        MaxZoom = 18;
    }

    private List<Team> _allTeams = [];

    public List<Team> Teams => MarkersList.AsTeamList();

    public IEnumerable<District> Districts => DistrictsLayer.Items;

    public async Task AddActor<TActor>(Coordinate? position, TActor actor) where TActor : Actor
    {
        position ??= await GetCurrentGeoLocation();

        if (position is not { } coord) return;

        await actor.UpdateLocation(coord);

        MarkersList.Add(actor);
    }

    //public async Task SetDistrictOwner(string districtName, string teamName)
    //{
    //    // Update the data store
    //    await _dataStore.SetDistrictOwnerAsync(districtName, teamName);
        
    //    // Update the visual representation
    //    var newOwner = _allTeams.Find(t => t.Name == teamName);
    //    if (newOwner is null) return;
        
    //    await DistrictsLayer.SetOwnerFor(districtName, newOwner);
    //}

    /// <summary>
    ///     Returns the district if the current position is inside its triggerCircle.
    /// </summary>
    /// <returns></returns>
    public async Task<District?> TryGetDistrictAtCurrentLocation(Coordinate? forcedCoordinate = null)
    {
        var location = forcedCoordinate
                       ?? await GetCurrentGeoLocation()
                       ?? Coordinate.Empty;

        return Districts.FirstOrDefault(d => location.DistanceTo(d.TriggerCircle.Center) * 1000 < d.TriggerCircle.Radius);
    }

    public void ClearCops()
    {
        MarkersList.RemoveRange(MarkersList.OfType<Cop>());
    }

    public void ClearTeams()
    {
        MarkersList.RemoveRange(MarkersList.OfType<Team>().ToList());
    }

    public Task SetTeamsDataAsync(TeamData[] teamsData)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            _allTeams = [.. teamsData.Select(td => new Team(td))];
            sw.Stop();

            Debug.WriteLine($"Loading Teams took {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        
        return Task.CompletedTask;
    }

    public async Task SetMapDataAsync(MapData mapData)
    {
        try
        {
            //var sw = Stopwatch.StartNew();
            await MapLayer.InitializeWithData([mapData], this);
            await DistrictsLayer.InitializeWithData(mapData.Districts, this, MapStyles.SelectedDistrictStyle);

            //await DistrictsLayer.SetSelectionStyles(this, MapStyles.SelectedDistrictStyle);
            //sw.Stop();

            //Debug.WriteLine($"Loading mapdata took {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

}