﻿namespace Konqvist.Web.Models.Layers;

public class DistrictsLayer : Layer
{
    private readonly MapDataStore _mapDataStore;
    private List<District> _districts = [];
    private readonly IBindableHubClient _hubClient;
    private readonly SessionProvider _sessionProvider;

    public DistrictsLayer(MapDataStore mapDataStore, IBindableHubClient hubClient, SessionProvider sessionProvider)
    {
        _mapDataStore = mapDataStore;
        _hubClient = hubClient;
        _sessionProvider = sessionProvider;

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
        if (!_sessionProvider.Session.IsPlayer)
        {
            Console.WriteLine("*** Not a player, cannot claim district");
            return false;
        }
        
        var foundDistrict = _districts
            .FirstOrDefault(d => d.IsAtLocation(location));

        Console.WriteLine($">>> Try claim district {foundDistrict?.Name ?? "NO DISTRICT"} by team '{teamName}'");
        if (foundDistrict is null || foundDistrict.Owner?.Name == teamName && foundDistrict.IsClaimable == false) return false;

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
        var districts = await _mapDataStore.GetAllDistricts();
        _districts = [.. districts.Select(d => new District(d))];
        ShapesList.Clear();
        ShapesList.AddRange(_districts);
    }
}
