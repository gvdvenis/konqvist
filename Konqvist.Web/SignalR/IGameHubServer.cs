namespace Konqvist.Web.SignalR;

public interface IGameHubServer
{
    Task BroadcastActorMove(ActorLocation actorLocation);
    
    /// <summary>
    ///     Signals all clients that a new runner should be added to the map.
    /// </summary>
    /// <returns></returns>
    Task BroadcastRunnerLogin();

    Task BroadcastRunnerLogout();

    Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner);

    /// <summary>
    ///     Send a runner logout request to all clients. Optionally provide
    ///     a team name to only log out a single teams runner
    /// </summary>
    /// <param name="teamName"></param>
    /// <returns></returns>
    Task SendRunnerLogoutRequest(string? teamName = null);
}