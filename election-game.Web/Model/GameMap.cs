using System;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.RenderTree;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class GameMap : OpenStreetMap
{ 
    private const string GamemapLayerName = "gameMap";
    private DistrictsLayer? _districtsLayer;

    private Func<Shape, StyleOptions?> GetShapeStyle { get; set; }

    private Func<StyleOptions?> GetSelectionStyle { get; set; }

    private StyleOptions? GetDefaultShapeStyle(Shape arg)
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

    private StyleOptions? GetDefaultSelectionStyle()
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
    }

    public List<District> Districts => _districtsLayer?.Districts ?? [];

    public async Task AddTeamMarker(Coordinate? position = null)
    {
        position ??= await GetCurrentGeoLocation();

        if (position is null) return;

        Team myTeam = new(position.Value);

        MarkersList.Clear();
        MarkersList.Add(myTeam);
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

public class Team : Marker
{
    public Team(Coordinate position)
    {
        Type = MarkerType.MarkerPin;
        Coordinates = new Coordinates(position);
        PinColor = PinColor.Blue;
        UpdateCoordinates();
    }
}

public class DistrictsLayer: Layer
{
    public DistrictsLayer(Func<Shape, StyleOptions?> getShapeStyle, string shapeData)
    {
        Id = "districts";
        LayerType = LayerType.Vector;
        SourceType = SourceType.VectorGeoJson;
        RaiseShapeEvents = true;
        Projection = "EPSG:4326";
        StyleCallback = getShapeStyle;
        SelectionEnabled = true;
        Declutter = true;
        Data = shapeData;
    }

    private bool Predicate(District s)
    {
        return s.RegionType != "election-district";
    }

    public List<District> Districts => ShapesList.Select(s=>new District(s.Coordinates, s.Properties)).ToList();
}

public class District : Polygon
{
    public District(Coordinates coordinates, Dictionary<string, dynamic> properties) : base(coordinates[0])
    {
        foreach ((string? key, dynamic? value) in properties)
        {
            Properties.TryAdd(key, value);
        }

    }

    public string Name => Properties.GetValueOrDefault("key")?.ToString() ?? "-";
    public string RegionType => Properties.GetValueOrDefault("region-type")?.ToString() ?? "-";

    #region Overrides of Object

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }

    #endregion
}
