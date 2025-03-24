using election_game.Data.Contracts;
using election_game.Data.Stores;
using ElectionGame.Web.SignalR;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class DistrictsLayer : Layer
{
    private readonly MapDataStore _mapDataStore;
    private List<District> _districts = [];
    private readonly IBindableHubClient _hubClient;

    public DistrictsLayer(MapDataStore mapDataStore, IBindableHubClient hubClient)
    {
        _mapDataStore = mapDataStore;
        _hubClient = hubClient;
        Id = nameof(DistrictsLayer);
        SelectionStyle = MapStyles.SelectedDistrictStyle;
        LayerType = LayerType.Vector;
        SelectionEnabled = true;

        hubClient.OnDistrictOwnerChanged += DistrictOwnerChanged;
        SelectionChanged = EventCallback.Factory.Create<SelectionChangedArgs>(this, ShowPopup);
    }

    private static async Task ShowPopup(SelectionChangedArgs sca)
    {
        if (sca.SelectedShapes.OfType<District>().FirstOrDefault() is { } district)
        {
            await district.ShowPopup();
        }
    }

    public async Task<bool> TryClaimDistrict(Coordinate location, string teamName)
    {
        var foundDistrict = _districts
            .FirstOrDefault(d => d.IsAtLocation(location));

        Console.WriteLine($">>> Try claim district {foundDistrict?.Name ?? "NO DISTRICT"} by team '{teamName}'");
        if (foundDistrict is null || foundDistrict.Owner?.Name == teamName) return false;

        Console.WriteLine(">>> Broadcast District Owner Changed");
        await _hubClient.BroadcastDistrictOwnerChange(new DistrictOwner(teamName, foundDistrict.Name));

        return true;
    }

    private async Task DistrictOwnerChanged(DistrictOwner districtOwner)
    {
        await InitLayer();
        Console.WriteLine($"<<< District '{districtOwner.DistrictName}' now owned by {districtOwner.TeamName}");
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await InitLayer();
    }

    private async Task InitLayer()
    {
        var districts = await _mapDataStore.GetAllDistrictsAsync();
        _districts = districts.Select(d => new District(d)).ToList();
        ShapesList.Clear();
        ShapesList.AddRange(_districts);
    }
}
