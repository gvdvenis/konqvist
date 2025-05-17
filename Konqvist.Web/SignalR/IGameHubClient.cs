namespace Konqvist.Web.SignalR;

public interface IGameHubClient
{
    Task DistrictOwnerChanged(DistrictOwner districtOwner);

    Task ActorMoved(ActorLocation actorLocation);

    Task RunnerLoggedInOrOut();

    Task RunnerLoggedIn(string teamName);

    Task RunnersLoggedOut(string[] teamName);

    Task PerformRunnerLogoutOnClient(string? teamName);

    Task NewRoundStarted(RoundData newRoundNumber);

    Task TeamResourcesChanged(string? teamName = null);

    Task VotesUpdated(Dictionary<string, int> votes);
}