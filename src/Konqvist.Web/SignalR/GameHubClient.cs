using Konqvist.Web.Services;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Konqvist.Web.SignalR;

/// <summary>
///     Client for interacting with the game hub via SignalR.
/// </summary>
public class GameHubClient : IBindableHubClient, IAsyncDisposable
{
    private readonly HubConnection _hubConnection;
    private readonly SessionProvider _sessionProvider;
    private readonly NavigationManager _navigationManager;
    private readonly IToastService _toastService;
    private readonly GameModeRoutingService _routingService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GameHubClient"/> class. This
    ///     is a strongly typed client for easier interacting with the game hub.
    /// </summary>
    /// <param name="navigationManager">The navigation manager to get the base URI.</param>
    /// <param name="sessionProvider"></param>
    /// <param name="toastService"></param>
    /// <param name="routingService"></param>
    public GameHubClient(
        IToastService toastService,
        NavigationManager navigationManager,
        SessionProvider sessionProvider,
        GameModeRoutingService routingService)
    {
        string hubUrl = navigationManager.BaseUri.TrimEnd('/') + GameHubServer.HubUrl;
        _navigationManager = navigationManager;
        _sessionProvider = sessionProvider;
        _toastService = toastService;
        _routingService = routingService;

        _hubConnection ??= new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();

        SubscribeClientHandlers();

        _hubConnection.StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Registers the events for the hub connection.
    /// </summary>
    private void SubscribeClientHandlers()
    {
        _hubConnection.On<DistrictOwner>(nameof(DistrictOwnerChanged), DistrictOwnerChanged);
        _hubConnection.On<ActorLocation>(nameof(ActorMoved), ActorMoved);
        _hubConnection.On(nameof(RunnerLoggedInOrOut), RunnerLoggedInOrOut);
        _hubConnection.On<string>(nameof(PerformRunnerLogoutOnClient), PerformRunnerLogoutOnClient);
        _hubConnection.On<string>(nameof(RunnerLoggedIn), RunnerLoggedIn);
        _hubConnection.On<string[]>(nameof(RunnersLoggedOut), RunnersLoggedOut);
        _hubConnection.On<RoundData>(nameof(NewRoundStarted), NewRoundStarted);
        _hubConnection.On<List<TeamVote>, string?>(nameof(VotesUpdated), VotesUpdated);
        _hubConnection.On<string>(nameof(TeamResourcesChanged), TeamResourcesChanged);
    }

    #region IGameHubServer implements

    public Task BroadcastNewLocation(ActorLocation actorLocation) =>
        _hubConnection.SendAsync(nameof(BroadcastNewLocation), actorLocation);

    public Task BroadcastActorMove(ActorLocation actorLocation) =>
        _hubConnection.SendAsync(nameof(BroadcastActorMove), actorLocation);

    public Task BroadcastRunnerLogin(string teamName) =>
        _hubConnection.SendAsync(nameof(BroadcastRunnerLogin), teamName);

    public Task BroadcastRunnersLogout(string[] teamNames) =>
        _hubConnection.SendAsync(nameof(BroadcastRunnersLogout), teamNames);

    public Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner) =>
        _hubConnection.SendAsync(nameof(BroadcastDistrictOwnerChange), districtOwner);

    public Task SendRunnerLogoutRequest(string? teamName = null) =>
        _hubConnection.SendAsync(nameof(SendRunnerLogoutRequest), teamName);

    public Task SendStartNewRoundRequest() =>
        _hubConnection.SendAsync(nameof(SendStartNewRoundRequest));

    public Task SendSetAdditionalResourcesRequest(string teamName, ResourcesData additionalResources) =>
        _hubConnection.SendAsync(nameof(SendSetAdditionalResourcesRequest), teamName, additionalResources);

    public Task SendResetGameRequest() =>
        _hubConnection.SendAsync(nameof(SendResetGameRequest));

    public Task SendCastVoteRequest(string receivingTeamName, string castingTeamName) =>
        _hubConnection.SendAsync(nameof(SendCastVoteRequest), receivingTeamName, castingTeamName);

    #endregion

    #region IGameHubClient implements

    public Task DistrictOwnerChanged(DistrictOwner districtOwner) => OnDistrictOwnerChanged?.Invoke(districtOwner) ?? Task.CompletedTask;

    public Task ActorMoved(ActorLocation actorLocation) => OnActorMoved?.Invoke(actorLocation) ?? Task.CompletedTask;

    public Task RunnerLoggedInOrOut() => OnRunnerLoggedInOrOut?.Invoke() ?? Task.CompletedTask;

    public Task RunnerLoggedIn(string teamName) => OnRunnerLoggedIn?.Invoke(teamName) ?? Task.CompletedTask;

    public Task RunnersLoggedOut(string[] teamNames) => OnRunnersLoggedOut?.Invoke(teamNames) ?? Task.CompletedTask;

    public Task PerformRunnerLogoutOnClient(string? teamName)
    {
        var session = _sessionProvider.Session;
        if ((!session.IsPlayer || teamName is not null) && session.TeamName != teamName)
            return Task.CompletedTask;
        _toastService.ShowWarning("The game master has logged you out", 4000);
        _navigationManager.NavigateTo("logout", false, true);
        return Task.CompletedTask;
    }

    public async Task NewRoundStarted(RoundData newRound)
    {
        if (_sessionProvider.Session.IsAdmin == false &&
            await _routingService.GetGameStateRoutePath(newRound.Kind) is { } routePath)
            _navigationManager.NavigateTo(routePath, false, true);
        if (OnNewRoundStarted is null) return;
        await OnNewRoundStarted.Invoke(newRound);
    }

    public Task TeamResourcesChanged(string? teamName) => 
        OnTeamResourcesChanged?.Invoke(teamName) ?? Task.CompletedTask;

    public Task VotesUpdated(List<TeamVote> votes, string? castingTeamName) => 
        OnVotesUpdated?.Invoke(votes, castingTeamName) ?? Task.CompletedTask;

    #endregion

    #region IBindableHubClient implements

    public Func<Task>? OnRunnerLoggedInOrOut { get; set; }

    public Func<string, Task>? OnRunnerLoggedIn { get; set; }

    public Func<string[], Task>? OnRunnersLoggedOut { get; set; }

    public Func<ActorLocation, Task>? OnActorMoved { get; set; }

    public Func<DistrictOwner, Task>? OnDistrictOwnerChanged { get; set; }

    public Func<RoundData, Task>? OnNewRoundStarted { get; set; }

    public Func<string?, Task>? OnTeamResourcesChanged { get; set; }

    public Func<List<TeamVote>, string?, Task>? OnVotesUpdated { get; set; }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
