namespace Konqvist.Web.SignalR;

public interface IGameHubClient
{
   Task DistrictOwnerChanged(DistrictOwner districtOwner);

   Task ActorMoved(ActorLocation actorLocation);

   Task RunnerLoggedInOrOut();
}