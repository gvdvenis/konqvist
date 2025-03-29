namespace ElectionGame.Web.Model;

public class TeamsLayer : Layer
{
    private readonly MapDataStore _mapDataStore;
    private readonly IBindableHubClient _hubClient;

    public TeamsLayer(MapDataStore mapDataStore, IBindableHubClient hubClient)
    {
        _mapDataStore = mapDataStore;
        _hubClient = hubClient;

        _hubClient.OnActorMoved += OnActorMoved;
        _hubClient.OnNewRunnerLoggedIn += InitLayer;

        Id = nameof(TeamsLayer);
        LayerType = LayerType.Vector;
        SelectionEnabled = false;
    }

    private async Task OnActorMoved(ActorLocation arg)
    {
        if (ShapesList.OfType<Team>().FirstOrDefault(t => t.Name == arg.Name) is { } localTeam)
        {
            await localTeam.UpdateLocation(arg.Location);
        }
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await InitLayer();
    }

    private async Task InitLayer()
    {
        var teamData = await _mapDataStore.GetTeams(true);
        var teams = teamData.Select(td => new Team(td));
        ShapesList.AddRange(teams);
    }

    #endregion

    public async Task BroadcastNewLocation(string teamName, Coordinate newLocation)
    {
        await _hubClient.BroadcastActorMove(new ActorLocation(teamName, newLocation));
    }
}
