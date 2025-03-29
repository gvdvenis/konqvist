using Microsoft.AspNetCore.SignalR.Client;

namespace ElectionGame.Web.SignalR;

/// <summary>
///     Client for interacting with the game hub via SignalR.
/// </summary>
public class GameHubClient : IBindableHubClient, IAsyncDisposable
{
    private readonly HubConnection _hubConnection;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GameHubClient"/> class. This
    ///     is a strongly typed client for easier interacting with the game hub.
    /// </summary>
    /// <param name="navigationManager">The navigation manager to get the base URI.</param>
    public GameHubClient(NavigationManager navigationManager)
    {
        string hubUrl = navigationManager.BaseUri.TrimEnd('/') + GameHubServer.HubUrl;

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

        _hubConnection.On<Task>(nameof(NewRunnerLoggedIn), NewRunnerLoggedIn);
    }

    #region IGameHubServer implements

    public Task BroadcastNewLocation(ActorLocation actorLocation) =>
        _hubConnection.SendAsync(nameof(BroadcastNewLocation), actorLocation);

    public Task BroadcastActorMove(ActorLocation actorLocation) =>
        _hubConnection.SendAsync(nameof(BroadcastActorMove), actorLocation);

    public Task BroadcastNewRunnerLogin() => 
        _hubConnection.SendAsync(nameof(BroadcastNewRunnerLogin));

    public Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner) =>
        _hubConnection.SendAsync(nameof(BroadcastDistrictOwnerChange), districtOwner);

    #endregion


    #region IGameHubClient implements

    public Task DistrictOwnerChanged(DistrictOwner districtOwner) => OnDistrictOwnerChanged?.Invoke(districtOwner) ?? Task.CompletedTask;

    public Task ActorMoved(ActorLocation actorLocation) => OnActorMoved?.Invoke(actorLocation) ?? Task.CompletedTask;

    public Task NewRunnerLoggedIn() => OnNewRunnerLoggedIn?.Invoke() ?? Task.CompletedTask;

    #endregion


    #region IBindableHubClient implements

    public Func<Task>? OnNewRunnerLoggedIn { get; set; }

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
