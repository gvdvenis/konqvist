using ElectionGame.Web.Authentication;

namespace ElectionGame.Web.Model;

public class TeamsLayer : Layer
{
    private readonly MapDataStore _mapDataStore;
    private readonly IBindableHubClient _hubClient;
    private readonly SessionProvider _sessionProvider;

    public TeamsLayer(MapDataStore mapDataStore, IBindableHubClient hubClient, SessionProvider sessionProvider)
    {
        _mapDataStore = mapDataStore;
        _hubClient = hubClient;
        _sessionProvider = sessionProvider;

        _hubClient.OnActorMoved += OnActorMoved;
        _hubClient.OnRunnerLoggedInOrOut += InitLayer;

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
        ShapesList.Clear();
        var teamData = await _mapDataStore.GetTeams(true);
        var teams = teamData.Select(td => new Team(td));
        ShapesList.AddRange(teams);
    }

    #endregion

    public async Task BroadcastNewLocation(string teamName, Coordinate newLocation)
    {
        if (_sessionProvider.Session.IsPlayer)
            await _hubClient.BroadcastActorMove(new ActorLocation(teamName, newLocation));
    }
}
