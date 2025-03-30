using election_game.Data.Models;

namespace ElectionGame.Web.Model;

public class TriggerCirclesLayer : Layer
{
    private readonly MapDataStore _mapDataStore;

    public TriggerCirclesLayer(MapDataStore mapDataStore, IBindableHubClient hubClient)
    {
        _mapDataStore = mapDataStore;
        Id = nameof(TriggerCirclesLayer);
        LayerType = LayerType.Vector;
        SelectionEnabled = false;

        hubClient.OnDistrictOwnerChanged += DistrictOwnerChanged;
    }

    private Task DistrictOwnerChanged(DistrictOwner districtOwner)
    {
        if (districtOwner == DistrictOwner.Empty)
        {
            // we recreate all claimable district circles on the map
            return InitLayer();
        }

        var triggerCircleToRemove = ShapesList
            .OfType<TriggerCircle>()
            .FirstOrDefault(tc => tc.DistrictName == districtOwner.DistrictName);

        if (triggerCircleToRemove is not null) ShapesList.Remove(triggerCircleToRemove);

        return Task.CompletedTask;
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
        ShapesList.Clear();

        var districts = await _mapDataStore.GetAllDistricts();

        var circles = districts
            .Where(d => d.IsClaimable)
            .Select(d => new TriggerCircle(d));

        ShapesList.AddRange(circles);
    }
}