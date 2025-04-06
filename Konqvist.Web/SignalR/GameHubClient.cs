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

    /// <summary>
    ///     Initializes a new instance of the <see cref="GameHubClient"/> class. This
    ///     is a strongly typed client for easier interacting with the game hub.
    /// </summary>
    /// <param name="navigationManager">The navigation manager to get the base URI.</param>
    /// <param name="sessionProvider"></param>
    /// <param name="toastService"></param>
    public GameHubClient(NavigationManager navigationManager, SessionProvider sessionProvider, IToastService toastService)
    {
        string hubUrl = navigationManager.BaseUri.TrimEnd('/') + GameHubServer.HubUrl;
        _navigationManager = navigationManager;
        _sessionProvider = sessionProvider;
        _toastService = toastService;

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
        _hubConnection.On<DistrictOwner, Task>(nameof(DistrictOwnerChanged), DistrictOwnerChanged);

        _hubConnection.On<ActorLocation, Task>(nameof(ActorMoved), ActorMoved);

        _hubConnection.On<Task>(nameof(RunnerLoggedInOrOut), RunnerLoggedInOrOut);

        _hubConnection.On<string, Task>(nameof(PerformRunnerLogoutOnClient), PerformRunnerLogoutOnClient);

        _hubConnection.On<string, Task>(nameof(RunnerLoggedIn), RunnerLoggedIn);

        _hubConnection.On<string[], Task>(nameof(RunnersLoggedOut), RunnersLoggedOut);
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
            
        _navigationManager.NavigateTo("logout", false);

        return Task.CompletedTask;
    }

    #endregion

    #region IBindableHubClient implements

    public Func<Task>? OnRunnerLoggedInOrOut { get; set; }

    public Func<string, Task>? OnRunnerLoggedIn { get; set; }
    
    public Func<string[], Task>? OnRunnersLoggedOut { get; set; }
    
    public Func<ActorLocation, Task>? OnActorMoved { get; set; }

    public Func<DistrictOwner, Task>? OnDistrictOwnerChanged { get; set; }
    

    public async Task StartAsync() => await _hubConnection.StartAsync();

    public async Task StopAsync() => await _hubConnection.StopAsync();

    #endregion

    #region IDisposable

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
    }

    #endregion
}
