namespace ElectionGame.Web.SignalR;

public interface IBindableHubClient: IGameHubClient, IGameHubServer
{
    Func<DistrictOwner, Task>? OnDistrictOwnerChanged { get; set; }
   
    Func<ActorLocation, Task>? OnActorMoved { get; set; }

    Func<Task>? OnRunnerLoggedInOrOut { get; set; }

    Task StartAsync();

    Task StopAsync();
}