using System.Diagnostics;
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
        gameMap.LayersList.Remove(this);

        ShapesList.Clear();
        Data = shapeData;
        gameMap.LayersList.Add(this);

        await UpdateLayer();
        await gameMap.UpdateLayer(this);
        await gameMap.SetSelectionSettings(this, true, MapStyles.SelectedDistrictStyle, false);
    }
    
    private static StyleOptions GetDistrictStyle(Shape arg)
    {
        return arg.ToDistrict() is not { } district
            ? new StyleOptions()
            : MapStyles.DistrictOwnerStyle(district.Owner);
    }

    public List<District> Districts => ShapesList.ToDistrictList();

}