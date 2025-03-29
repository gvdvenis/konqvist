namespace ElectionGame.Web.SignalR;

public interface IGameHubServer
{
    Task BroadcastActorMove(ActorLocation actorLocation);
    
    /// <summary>
    ///     Signals all clients that a new runner should be added to the map.
    /// </summary>
    /// <returns></returns>
    Task BroadcastNewRunnerLogin();

    Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner);
}