using election_game.Data.Contracts;
using election_game.Data.Stores;
using ElectionGame.Web.SignalR;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

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
        var triggerCircleToRemove = ShapesList.OfType<TriggerCircle>().FirstOrDefault(tc => tc.DistrictName == districtOwner.DistrictName);

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
        var districts = await _mapDataStore.GetAllDistricts();
        var triggerCircles = districts
            .Where(d=> d.IsClaimable)
            .Select(d => new TriggerCircle(d));
        ShapesList.AddRange(triggerCircles);
    }
}