using election_game.Data.Model.MapElements;
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
    }

    public List<District> Districts => ShapesList.OfType<District>().ToList();

    public async Task AddDistricts(List<DistrictData> mapDataDistricts, GameMap gameMap)
    {
        if (!gameMap.LayersList.Contains(this)) gameMap.LayersList.Add(this);
        
        ShapesList.Clear();

        foreach (var district in mapDataDistricts.Select(districtData => new District(districtData)))
        {
            ShapesList.AddRange([
                district, 
                district.TriggerCircle
            ]);
        }

        await gameMap.UpdateLayer(this);
        await gameMap.SetSelectionSettings(this, true, MapStyles.SelectedDistrictStyle, false);
    }
}