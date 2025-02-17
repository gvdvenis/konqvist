using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class DistrictsLayer : ReactiveLayer
{
    public DistrictsLayer()
    {
        Id = "districts";
        SourceType = SourceType.VectorGeoJson;
        Projection = "EPSG:4326";
        SelectionEnabled = true;
        StyleCallback = GetDistrictStyle;
    }

    public async Task SetJsonData(string shapeData, GameMap gameMap)
    {
        Map = gameMap;
        ShapesList.Clear();
        Data = shapeData;
        await Map.SetSelectionSettings(this, true, MapStyles.SelectedDistrictStyle, false);
        await UpdateLayer();
    }
    
    private static StyleOptions GetDistrictStyle(Shape arg)
    {
        return arg.ToDistrict() is not { } district
            ? new StyleOptions()
            : MapStyles.DistrictOwnerStyle(district.Owner);
    }

    public List<District> Districts => ShapesList.ToDistrictList();

    #region Overrides of ReactiveLayer

    /// <inheritdoc />
    protected override bool IncludeInLayer(Shape shape)
    {
        return shape.IsDistrict();
    }

    #endregion
}