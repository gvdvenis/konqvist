using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class GameMap : OpenStreetMap
{
    private DistrictsLayer DistrictsLayer { get; set; } = new();

    private CopsLayer CopsLayer { get; set; } = new();

    public GameMap()
    {
        Center = new Coordinate([6.261195479378347, 51.87638698662113]);
        Zoom = 16;
        MinZoom = 14;
        MaxZoom = 18;

        LayersList.Add(CopsLayer);
        LayersList.Add(DistrictsLayer);
    }

    public List<Team> Teams => MarkersList.AsTeamList();

    public List<District> Districts => DistrictsLayer.Districts;

    /// <summary>
    ///     Add a team marker to the game map. If no <paramref name="position"/> is provided,
    ///     an attempt will be made to use the current location.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    public async Task AddTeam(string name, Coordinate? position = null)
    {
        position ??= await GetCurrentGeoLocation();

        if (position is null) return;

        Team myTeam = new(position.Value, name);
        
        MarkersList.Add(myTeam);
    }

    public async Task AddCop(Coordinate? position = null)
    {
        position ??= await GetCurrentGeoLocation();

        if (position is null) return;

        Cop aCop = new(position.Value);
        
        ShapesList.Remove(ShapesList.OfType<Cop>().Last());
        ShapesList.Add(aCop);
        CopsLayer.ShapesList.Add(aCop);
        await CopsLayer.Show();
        
        await UpdateLayer(CopsLayer);
        //MarkersList.Add(aCop);
    }

    public async Task HideCops()
    {
        await CopsLayer.Hide();
        await UpdateLayer(CopsLayer);
    }

    public async Task ShowCops()
    {
        await CopsLayer.Show();
        await UpdateLayer(CopsLayer);
    }

    public void ClearCops()
    {
        MarkersList.RemoveRange(MarkersList.OfType<Cop>());
    }

    public void ClearTeams()
    {
        MarkersList.RemoveRange(MarkersList.OfType<Team>().ToList());
    }

    public async Task LoadMapDataAsync(string jsonData)
    {
        
        await DistrictsLayer.SetJsonData(jsonData, this);

        LayersList.Remove(DistrictsLayer);
        LayersList.Add(DistrictsLayer);
        try
        {
            await UpdateLayer(DistrictsLayer);
            await SetSelectionSettings(DistrictsLayer, true, MapStyles.SelectedDistrictStyle, false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            
        }
    }
}