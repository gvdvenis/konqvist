using Microsoft.AspNetCore.SignalR;

namespace ElectionGame.Web.SignalR;

public class GameHubServer(MapDataStore dataStore) : Hub<IGameHubClient>, IGameHubServer
{
    public const string HubUrl = "/chat";

    public async Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner)
    {
        // Update the data store first
        await dataStore.SetDistrictOwner(districtOwner);

        // Then broadcast to clients
        Console.WriteLine($"+++ Change district owner for '{districtOwner.DistrictName}' to {districtOwner.TeamName}");
        await Clients.All.DistrictOwnerChanged(districtOwner);
    }

    /// <inheritdoc />
    public async Task BroadcastNewRunnerLogin()
    {
        await Clients.All.NewRunnerLoggedIn();
    }
    
    /// <summary>
    ///     Broadcasts the new location of an actor
    /// </summary>
    /// <param name="actorLocation"></param>
    /// <returns></returns>
    public async Task BroadcastActorMove(ActorLocation actorLocation)
    {
        await dataStore.UpdateTeamPosition(actorLocation.Name, actorLocation.Location);
        await Clients.All.ActorMoved(actorLocation);
    }
    
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"+++ {Context.ConnectionId} connected");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        Console.WriteLine($"--- Disconnected {e?.Message} {Context.ConnectionId}");
        await base.OnDisconnectedAsync(e);
    }
}