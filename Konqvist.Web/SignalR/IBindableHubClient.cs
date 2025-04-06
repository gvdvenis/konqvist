namespace Konqvist.Web.SignalR;

public interface IBindableHubClient: IGameHubClient, IGameHubServer
{
    Func<DistrictOwner, Task>? OnDistrictOwnerChanged { get; set; }
   
    Func<ActorLocation, Task>? OnActorMoved { get; set; }
    
    Func<Task>? OnRunnerLoggedInOrOut { get; set; }

    Func<string, Task>? OnRunnerLoggedIn { get; set; }

    Func<string[], Task>? OnRunnersLoggedOut { get; set; }

    Task StartAsync();

    Task StopAsync();
}