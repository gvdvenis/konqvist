namespace ElectionGame.Web.Model;

public class MapLayer : Layer
{
    private readonly MapDataStore _mapDataStore;

    public MapLayer(MapDataStore mapDataStore)
    {
        _mapDataStore = mapDataStore;
        Id = nameof(MapLayer);
        LayerType = LayerType.Vector;
        SelectionEnabled = false;
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await InitLayer();
    }

    private async Task InitLayer()
    {
        var mapData = await _mapDataStore.GetMapData();
        var mapShape = new Polygon(mapData.Coordinates.ToList());
        ShapesList.AddRange([mapShape]);
    }

    #endregion
}