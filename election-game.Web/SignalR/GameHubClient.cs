using election_game.Data.Contracts;
using election_game.Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace ElectionGame.Web.SignalR;

/// <summary>
///     Client for interacting with the game hub via SignalR.
/// </summary>
public class GameHubClient : IGameHubClient, IGameHubServer
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

        RegisterEvents();
    }

    /// <summary>
    ///     Registers the events for the hub connection.
    /// </summary>
    private void RegisterEvents()
    {
        _hubConnection.On<string, Task>(nameof(UserDisconnected), UserDisconnected.InvokeAsync);
        
        _hubConnection.On<MapData, Task>(nameof(InitializeMapData), InitializeMapData.InvokeAsync);

        _hubConnection.On<TeamData[], Task>(nameof(InitializeTeamsData), InitializeTeamsData.InvokeAsync);

        _hubConnection.On<DistrictOwner, Task>(nameof(DistrictOwnerChanged), DistrictOwnerChanged.InvokeAsync);

        _hubConnection.On<ActorLocation, Task>(nameof(NewLocationReceived), NewLocationReceived.InvokeAsync);
    }

    #region IGameHubServer implements

    public Task BroadcastNewLocation(ActorLocation actorLocation) =>
        _hubConnection.SendAsync(nameof(IGameHubServer.BroadcastNewLocation), actorLocation);

    public Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner) =>
        _hubConnection.SendAsync(nameof(IGameHubServer.BroadcastDistrictOwnerChange), districtOwner);
    
    #endregion

    #region IGameHubClient implements
    
    /// <inheritdoc />
    public EventCallback<string> UserDisconnected { get; set; }

    /// <inheritdoc />
    public EventCallback<MapData> InitializeMapData { get; set; }

    /// <inheritdoc />
    public EventCallback<TeamData[]> InitializeTeamsData { get; set; }

    /// <inheritdoc />
    public EventCallback<DistrictOwner> DistrictOwnerChanged { get; set; }

    /// <inheritdoc />
    public EventCallback<ActorLocation> NewLocationReceived { get; set; }

    public async Task StartAsync() => await _hubConnection.StartAsync();

    public async Task StopAsync() => await _hubConnection.StopAsync();

    public IGameHubServer Server => this;

    #endregion
}
