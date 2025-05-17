namespace Konqvist.Web.SignalR;

public interface IGameHubServer
{
    Task BroadcastActorMove(ActorLocation actorLocation);

    /// <summary>
    ///     Signals all clients that a new runner should be added to the map.
    /// </summary>
    /// <returns></returns>
    Task BroadcastRunnerLogin(string teamName);

    /// <summary>
    ///     Signals all clients that one or more runners should be removed from the map.
    /// </summary>
    /// <param name="teamNames"></param>
    /// <returns></returns>
    Task BroadcastRunnersLogout(string[] teamNames);

    /// <summary>
    ///     Broadcasts that a district has changed owner and the client data should be updated accordingly.
    /// </summary>
    /// <param name="districtOwner"></param>
    /// <returns></returns>
    Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner);

    /// <summary>
    ///     Send a runner logout request to all clients. Optionally provide
    ///     a team name to only log out a single teams runner
    /// </summary>
    /// <param name="teamName"></param>
    /// <returns></returns>
    Task SendRunnerLogoutRequest(string? teamName = null);

    /// <summary>
    ///     Send a request that initiates a new round.
    /// </summary>
    /// <returns></returns>
    Task SendStartNewRoundRequest();

    /// <summary>
    ///     Sends a request to update a team's resources.
    /// </summary>
    /// <param name="teamName"></param>
    /// <param name="additionalResources"></param>
    /// <returns></returns>
    Task SendSetAdditionalResourcesRequest(string teamName, ResourcesData additionalResources);

    Task SendResetGameRequest();

    /// <summary>
    ///     Sends a request to cast a vote with a specified weight for a team.
    /// </summary>
    /// <param name="teamName"></param>
    /// <param name="voteWeight"></param>
    /// <returns></returns>
    Task SendCastVoteRequest(string teamName, int voteWeight);
}