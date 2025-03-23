using election_game.Data.Contracts;
using Microsoft.AspNetCore.SignalR;
using election_game.Data.Stores;

namespace ElectionGame.Web.SignalR;

public class GameHubServer(MapDataStore dataStore) : Hub<IGameHubClient>, IGameHubServer
{
    public const string HubUrl = "/chat";

    public async Task BroadcastNewLocation(ActorLocation actorLocation)
    {
        await Clients.All.NewLocationReceived(actorLocation); 
    }

    public async Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner)
    {
        // Update the data store first
        await dataStore.SetDistrictOwnerAsync(districtOwner);
        
        // Then broadcast to clients
        await Clients.All.InitializeMapData(await dataStore.GetMapDataAsync());
        //await Clients.All.DistrictOwnerChanged(districtOwner);
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"{Context.ConnectionId} connected");
        await base.OnConnectedAsync();

        // Send the map data to initialize the client
        var mapData = await dataStore.GetMapDataAsync();
        var teamsData = await dataStore.GetTeamsDataAsync();

        // Instruct the caller client to initialize the map and teams data
        await Clients.Caller.InitializeTeamsData(teamsData);
        //await Clients.Caller.InitializeMapData(mapData);
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        Console.WriteLine($"Disconnected {e?.Message} {Context.ConnectionId}");
        await base.OnDisconnectedAsync(e);

        await Clients.All.UserDisconnected(Context.ConnectionId);
    }
}