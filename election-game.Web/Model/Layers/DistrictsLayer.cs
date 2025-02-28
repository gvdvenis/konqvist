using election_game.Data.Model.MapElements;
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