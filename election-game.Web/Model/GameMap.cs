using System.Diagnostics;
using election_game.Data.Model.MapElements;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class GameMap : OpenStreetMap
{
    private DistrictsLayer DistrictsLayer { get; set; } = new();
    private MapLayer MapLayer { get; set; } = new();
    private CopsLayer CopsLayer { get; set; } = new();

    public GameMap()
    {
        Center = new Coordinate([6.261195479378347, 51.87638698662113]);
        Zoom = 16;
        MinZoom = 14;
        MaxZoom = 18;

        //LayersList.Add(CopsLayer);
        //LayersList.Add(DistrictsLayer);
    }

    public List<Team> Teams => MarkersList.AsTeamList();

    public List<District> Districts => DistrictsLayer.Items;

    public async Task AddActor<TActor>(Coordinate? position, TActor actor) where TActor : Actor
    {
        position ??= await GetCurrentGeoLocation();

        if (position is not { } coord) return;

        await actor.UpdateLocation(coord);

        MarkersList.Add(actor);
    }

    public void ClearCops()
    {
        MarkersList.RemoveRange(MarkersList.OfType<Cop>());
    }

    public void ClearTeams()
    {
        MarkersList.RemoveRange(MarkersList.OfType<Team>().ToList());
    }

    public async Task LoadMapDataAsync(MapData mapData)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            await MapLayer.InitializeWithData([mapData], this);
            await DistrictsLayer.InitializeWithData(mapData.Districts, this, MapStyles.SelectedDistrictStyle );

            sw.Stop();
            Debug.WriteLine(sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}