namespace Konqvist.Web.Models.Layers;

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
    }

    public async Task<bool> TryClaimDistrict(Coordinate location, string teamName)
    {
        if (!_sessionProvider.Session.IsPlayer)
        {
            Console.WriteLine("*** Not a player, cannot claim district");
            return false;
        }

        var foundDistrict = GetDistrictAtCoordinate(location);

        Console.WriteLine($">>> Try claim district {foundDistrict?.Name ?? "NO DISTRICT"} by team '{teamName}'");
        if (foundDistrict is null || // nothing to claim
            foundDistrict.Owner.Name == teamName && // already claimed by this team
            foundDistrict.IsClaimable == false) // not in a claimable state 
            return false;

        if (await _mapDataStore.GetCurrentAppState() != RoundKind.GatherResources)
            return false; // current game state does not allow claiming resources

        Console.WriteLine(">>> Broadcast District Owner Changed");
        await _hubClient.BroadcastDistrictOwnerChange(new DistrictOwner(teamName, foundDistrict.Name));

        return true;
    }

    public District? GetDistrictById(string id)
    {
        return _districts.FirstOrDefault(d => d.Id == id);
    }

    private District? GetDistrictAtCoordinate(Coordinate location)
    {
        return _districts
            .FirstOrDefault(d => d.IsAtLocation(location));
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
