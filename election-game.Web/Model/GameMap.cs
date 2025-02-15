using ElectionGame.Web.Model.Helpers;
using OpenLayers.Blazor;
using System.Xml.Linq;

namespace ElectionGame.Web.Model;

public class GameMap : OpenStreetMap
{
    private DistrictsLayer? _districtsLayer;

    private Func<Shape, StyleOptions?> GetShapeStyle { get; set; }

    private Func<StyleOptions?> GetSelectionStyle { get; set; }

    private StyleOptions GetDefaultShapeStyle(Shape arg)
    {
        return new StyleOptions
        {
            Stroke = new StyleOptions.StrokeOptions
            {
                Color = "red",
                Width = 3,
                LineDash = [4]
            },
            Fill = new StyleOptions.FillOptions
            {
                Color = "rgba(0, 255, 50, 0.5)"
            }
        };
    }

    private StyleOptions GetDefaultSelectionStyle()
    {
        return new StyleOptions
        {
            Stroke = new StyleOptions.StrokeOptions
            {
                Color = "blue",
                Width = 5,
                LineDash = [4]
            },
            Fill = new StyleOptions.FillOptions
            {
                Color = "rgba(255, 50, 50, 0.5)"
            }
        };
    }

    public GameMap()
    {
        Center = new Coordinate([6.261195479378347, 51.87638698662113]);
        Zoom = 16;
        MinZoom = 12;
        MaxZoom = 18;
        GetShapeStyle = GetDefaultShapeStyle;
        GetSelectionStyle = GetDefaultSelectionStyle;
        LayersList.Add(CopsLayer);
    }

    public List<Team> Teams => MarkersList.AsTeamList();

    public List<District> Districts => _districtsLayer?.Districts ?? [];

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

        CopsLayer.ShapesList.Add(aCop);

        MarkersList.Add(aCop);
    }

    public ReactiveLayer CopsLayer { get; private set; } = new();

    public void HideCops()
    {
        CopsLayer.Hide();
        
        UpdateLayer(CopsLayer);
    }

    public void ShowCops()
    {
        CopsLayer.Show();
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
        await SetSelectionSettings(AddDistrictsLayer(jsonData), true, GetSelectionStyle.Invoke(), false);
    }

    private DistrictsLayer AddDistrictsLayer(string jsonData)
    {
        var layer = LayersList.OfType<DistrictsLayer>().FirstOrDefault();

        if (layer is not null) LayersList.Remove(layer);

        var geoLayer = new DistrictsLayer(GetShapeStyle, jsonData);

        LayersList.Add(geoLayer);

        _districtsLayer = geoLayer;

        return geoLayer;
    }
}