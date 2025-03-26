using election_game.Data.Contracts;

namespace ElectionGame.Web.SignalR;

public interface IGameHubServer
{
    Task BroadcastActorMove(ActorLocation actorLocation);
    
    Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner);
}

public interface IGameHubClient
{
   Task DistrictOwnerChanged(DistrictOwner districtOwner);

   Task ActorMoved(ActorLocation actorLocation);
}

public interface IBindableHubClient: IGameHubClient, IGameHubServer
{
   Func<DistrictOwner, Task>? OnDistrictOwnerChanged { get; set; }
   
   Func<ActorLocation, Task>? OnActorMoved { get; set; }
   
   Task StartAsync();

   Task StopAsync();
}