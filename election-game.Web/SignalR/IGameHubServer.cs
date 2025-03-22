using election_game.Data.Contracts;

namespace ElectionGame.Web.SignalR;
public interface IGameHubServer
{
    Task BroadcastNewLocation(ActorLocation actorLocation);

    Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner);
}