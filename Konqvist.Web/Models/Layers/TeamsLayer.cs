using Microsoft.FluentUI.AspNetCore.Components;

namespace Konqvist.Web.Models.Layers;

public class TeamsLayer : Layer
{
    private readonly MapDataStore _mapDataStore;
    private readonly IBindableHubClient _hubClient;
    private readonly UserSession _session;

    public TeamsLayer(
        MapDataStore mapDataStore, 
        IBindableHubClient hubClient, 
        SessionProvider sessionProvider, 
        IToastService toastService)
    {
        _mapDataStore = mapDataStore;
        _hubClient = hubClient;
        _session = sessionProvider.Session;

        _hubClient.OnActorMoved += OnActorMoved;
        _hubClient.OnRunnerLoggedIn += AddRunner;
        _hubClient.OnRunnersLoggedOut += RemoveRunners;

        Id = nameof(TeamsLayer);
        LayerType = LayerType.Vector;
        SelectionEnabled = false;
    }

    private async Task AddRunner(string teamName)
    {
        var teamData = await _mapDataStore.GetTeamByName(teamName);
        var team = Team.CreateFromDataOrEmtpy(teamData);
        ShapesList.Add(team);
    }

    private Task RemoveRunners(string[] teamNames)
    {
        var teams = ShapesList
            .OfType<Team>()
            .Where(t => teamNames.Contains(t.Name));

        ShapesList.RemoveRange(teams);

        return Task.CompletedTask;
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
        if (_session.IsPlayer)
            await _hubClient.BroadcastActorMove(new ActorLocation(teamName, newLocation));
    }

    public async Task LogoutAllRunners()
    {
        if (_session.IsAdmin)
        {
            await _hubClient.SendRunnerLogoutRequest();
        }
    }
}
