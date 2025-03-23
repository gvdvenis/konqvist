using election_game.Data.Contracts;
using election_game.Data.Models;
using Microsoft.AspNetCore.Components;
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

        RegisterEvents();

        _hubConnection.StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Registers the events for the hub connection.
    /// </summary>
    private void RegisterEvents()
    {
        _hubConnection.On<string, Task>(nameof(UserDisconnected), UserDisconnected);
        
        _hubConnection.On<MapData, Task>(nameof(InitializeMapData), InitializeMapData);

        _hubConnection.On<TeamData[], Task>(nameof(InitializeTeamsData), InitializeTeamsData);

        _hubConnection.On<DistrictOwner, Task>(nameof(DistrictOwnerChanged), DistrictOwnerChanged);

        _hubConnection.On<ActorLocation, Task>(nameof(NewLocationReceived), NewLocationReceived);
    }

    #region IGameHubServer implements

    public Task BroadcastNewLocation(ActorLocation actorLocation) => 
        _hubConnection.SendAsync(nameof(BroadcastNewLocation), actorLocation);

    public Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner) =>
        _hubConnection.SendAsync(nameof(BroadcastDistrictOwnerChange), districtOwner);

    #endregion

    #region IGameHubClient implements

    /// <inheritdoc />
    public Task UserDisconnected(string userId)=> OnUserDisconnected.InvokeAsync(userId);

    /// <inheritdoc />
    public Task InitializeMapData(MapData mapData) => OnInitializeMapData.InvokeAsync(mapData);

    /// <inheritdoc />
    public Task InitializeTeamsData(TeamData[] teamData) => OnInitializeTeamsData.InvokeAsync(teamData);

    /// <inheritdoc />
    public Task DistrictOwnerChanged(DistrictOwner districtOwner) => OnDistrictOwnerChanged.InvokeAsync(districtOwner);

    /// <inheritdoc />
    public Task NewLocationReceived(ActorLocation actorLocation) => OnNewLocationReceived.InvokeAsync(actorLocation);


    /// <inheritdoc />
    public EventCallback<string> OnUserDisconnected { get; set; }

    /// <inheritdoc />
    public EventCallback<MapData> OnInitializeMapData { get; set; }

    /// <inheritdoc />
    public EventCallback<TeamData[]> OnInitializeTeamsData { get; set; }

    /// <inheritdoc />
    public EventCallback<DistrictOwner> OnDistrictOwnerChanged { get; set; }

    /// <inheritdoc />
    public EventCallback<ActorLocation> OnNewLocationReceived { get; set; }


    public async Task StartAsync() => await _hubConnection.StartAsync();

    public async Task StopAsync() => await _hubConnection.StopAsync();


    #endregion


    #region IDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
    }

    #endregion
}
