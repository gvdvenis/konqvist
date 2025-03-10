using election_game.Data.Model.MapElements;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class MapLayer : ReactiveLayer<MapData, Polygon>
{
    public MapLayer()
    {
        Id = "gameMapLayer";
    }

    #region Overrides of ReactiveLayer<MapData, Polygon>

    /// <inheritdoc />
    protected override Shape[] ShapeInitializer(MapData shapeData)
    {
        return [
            new Polygon(shapeData.Coordinates.ToList())
        ];
    }

    #endregion
}

public class DistrictsLayer : ReactiveLayer<DistrictData, District>
{

    public DistrictsLayer(): base()
    {
        Id = "districtsLayer";
        RaiseShapeEvents = true;
        SelectionEnabled = true;
        
        SelectedShapeChanged = EventCallback.Factory.Create<Shape>(this, LayerSelectedShapeChanged);
        SelectionChanged = EventCallback.Factory.Create<SelectionChangedArgs>(this, LayerSelectionChanged);
    }

    private static Task LayerSelectionChanged(SelectionChangedArgs arg)
    {
        return Task.CompletedTask;
    }

    private static async Task LayerSelectedShapeChanged(Shape shape)
    {
        if (shape is not District district) return;
        await district.ShowPopup();
    }

    #region Overrides of ReactiveLayer<DistrictData,District>

    /// <inheritdoc />
    protected override Shape[] ShapeInitializer(DistrictData shapeData)
    {
        var district = new District(shapeData);

        return [
            district,
            district.TriggerCircle
        ];
    }

    #endregion
}