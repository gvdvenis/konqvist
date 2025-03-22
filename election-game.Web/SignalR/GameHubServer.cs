using election_game.Data.Contracts;
using Microsoft.AspNetCore.SignalR;
using election_game.Data.Stores;

namespace ElectionGame.Web.SignalR;

public class GameHubServer : Hub<IGameHubClient>, IGameHubServer
{
    private readonly MapDataStore _dataStore;

    public GameHubServer(MapDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public const string HubUrl = "/chat";

    public async Task BroadcastNewLocation(ActorLocation actorLocation)
    {
        await Clients.All.NewLocationReceived.InvokeAsync(actorLocation); //NewLocationReceived(username, newLocation);
    }

    public async Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner)
    {
        // Update the data store first
        await _dataStore.SetDistrictOwnerAsync(districtOwner);
        
        // Then broadcast to clients
        await Clients.All.DistrictOwnerChanged.InvokeAsync(districtOwner);
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"{Context.ConnectionId} connected");
        await base.OnConnectedAsync();

        // Send the map data to initialize the client
        var mapData = await _dataStore.GetMapDataAsync();
        var teamsData = await _dataStore.GetTeamsDataAsync();

        // Instruct the caller client to initialize the map and teams data
        await Clients.Caller.InitializeMapData.InvokeAsync(mapData);
        await Clients.Caller.InitializeTeamsData.InvokeAsync(teamsData);
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        Console.WriteLine($"Disconnected {e?.Message} {Context.ConnectionId}");
        await base.OnDisconnectedAsync(e);

        await Clients.All.UserDisconnected.InvokeAsync(Context.ConnectionId);
    }
}