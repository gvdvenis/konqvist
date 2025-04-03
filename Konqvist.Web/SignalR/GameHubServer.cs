namespace Konqvist.Web.SignalR;

public class GameHubServer(MapDataStore dataStore) : Hub<IGameHubClient>, IGameHubServer
{
    public const string HubUrl = "/chat";
    //public const string HubUrl = "https://ass-konqvist-app.service.signalr.net";

    /// <inheritdoc />
    public async Task BroadcastRunnerLogout()
    {
        await Clients.All.RunnerLoggedInOrOut();
    }

    public async Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner)
    {
        // Update the data store first
        await dataStore.SetDistrictOwner(districtOwner);

        // Then broadcast to clients
        Console.WriteLine($"+++ Change district owner for '{districtOwner.DistrictName}' to {districtOwner.TeamName}");
        await Clients.All.DistrictOwnerChanged(districtOwner);
    }

    /// <summary>
    ///     Send a runner logout request to all clients. Optionally provide
    ///     a team name to only log out a single teams runner
    /// </summary>
    /// <param name="teamName"></param>
    /// <returns></returns>
    public async Task SendRunnerLogoutRequest(string? teamName = null)
    {
        if (teamName == null)
            await dataStore.LogoutAllRunners();
        else
            await dataStore.LogoutRunner(teamName);

        await Clients.All.RequestRunnerLogout(teamName);
    }

    /// <inheritdoc />
    public async Task BroadcastRunnerLogin()
    {
        await Clients.All.RunnerLoggedInOrOut();
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