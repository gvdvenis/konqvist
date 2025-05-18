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
            await Clients.All.PerformRunnerLogoutOnClient(null);
            await BroadcastRunnersLogout([.. loggedOutRunnerTeamNames]);
            return;
        }

        bool result = await dataStore.LogoutRunner(teamName);
        await Clients.All.PerformRunnerLogoutOnClient(teamName);
        if (result) await BroadcastRunnersLogout(teamName);
    }

    public async Task SendStartNewRoundRequest()
    {
        var nextRound = await dataStore.NextRound();

        if (nextRound == null)
        {
            Console.WriteLine("+++ No next round found");
            return;
        }

        Console.WriteLine($"+++ Start round number {nextRound.Index}");

        // Broadcast to all clients
        await Clients.All.NewRoundStarted(nextRound);
        await Clients.All.TeamResourcesChanged();
    }

    public async Task SendSetAdditionalResourcesRequest(string teamName, ResourcesData additionalResources)
    {
        await dataStore.SetAdditionalTeamResource(teamName, additionalResources);

        Console.WriteLine($"+++ Set additional resources for {teamName}");
        await Clients.All.TeamResourcesChanged(teamName);
    }

    public async Task SendResetGameRequest()
    {
        await dataStore.ResetGame();
        await SendRunnerLogoutRequest();

        Console.WriteLine($"+++ The game was reset to go from start again");
        await Clients.All.DistrictOwnerChanged(DistrictOwner.Empty);
        await Clients.All.TeamResourcesChanged();
        await Clients.All.NewRoundStarted(RoundData.Empty);
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

    public async Task SendCastVoteRequest(string receivingTeamName, int voteWeight, string castingTeamName)
    {
        // Add the vote to the store for the receiving team, from the casting team
        await dataStore.AddVoteForCurrentRound(receivingTeamName, castingTeamName, voteWeight);
        var votes = await dataStore.GetVotesForCurrentRound();

        // Broadcast the updated votes to all clients, include the casting team name
        await Clients.All.VotesUpdated(votes, castingTeamName);
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