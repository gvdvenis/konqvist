using election_game.Data.Stores;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class TriggerCirclesLayer : Layer
{
    private readonly MapDataStore _mapDataStore;

    public TriggerCirclesLayer(MapDataStore mapDataStore)
    {
        _mapDataStore = mapDataStore;
        Id = nameof(TriggerCirclesLayer);
        LayerType = LayerType.Vector;
        SelectionEnabled = false;
    }

    #region Overrides of ComponentBase

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await InitLayer();
    }

    #endregion

    private async Task InitLayer()
    {
        var mapData = await _mapDataStore.GetMapDataAsync();
        var triggerCircles = mapData.Districts.Select(d => new TriggerCircle(d.TriggerCircleCenter)).ToList();
        ShapesList.AddRange(triggerCircles);
    }
}