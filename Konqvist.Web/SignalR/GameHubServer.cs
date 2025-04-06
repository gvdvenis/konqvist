namespace Konqvist.Web.SignalR;

public class GameHubServer(MapDataStore dataStore) : Hub<IGameHubClient>, IGameHubServer
{
    public const string HubUrl = "/chat";
    //public const string HubUrl = "https://ass-konqvist-app.service.signalr.net";


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
        {
            var loggedOutRunnerTeamNames = await dataStore.LogoutAllRunners();
            await BroadcastRunnersLogout([..loggedOutRunnerTeamNames]);
            return;
        }

        bool result = await dataStore.LogoutRunner(teamName);
        await Clients.All.PerformRunnerLogoutOnClient(teamName);
        if (result) await BroadcastRunnersLogout(teamName);
    }

    public Task SendStartNewRoundRequest(int newRoundNumber)
    {
        Console.WriteLine($"+++ Start round number {newRoundNumber}");

        // Broadcast to all clients
        return Clients.All.BroadCastNewRoundStarted(newRoundNumber);

        return Task.CompletedTask;
    }

    public async Task BroadcastRunnersLogout(params string[] teamNames)
    {
        await Clients.All.RunnerLoggedInOrOut();
        await Clients.All.RunnersLoggedOut(teamNames);
    }

    public async Task BroadcastRunnerLogin(string teamName)
    {
        await Clients.All.RunnerLoggedInOrOut();
        await Clients.All.RunnerLoggedIn(teamName);
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