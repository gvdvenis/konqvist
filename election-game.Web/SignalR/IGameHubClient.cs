using election_game.Data.Contracts;
using election_game.Data.Models;
using Microsoft.AspNetCore.Components;

namespace ElectionGame.Web.SignalR;

public interface IGameHubServer
{
    public Task BroadcastNewLocation(ActorLocation actorLocation);
    public Task BroadcastDistrictOwnerChange(DistrictOwner districtOwner);
}

public interface IBindableHubClient: IGameHubClient, IGameHubServer
{
    public EventCallback<string> OnUserDisconnected { get; set; }
    
    public EventCallback<MapData> OnInitializeMapData { get; set; }

    public EventCallback<TeamData[]> OnInitializeTeamsData { get; set; }

    public EventCallback<DistrictOwner> OnDistrictOwnerChanged { get; set; }

    public EventCallback<ActorLocation> OnNewLocationReceived { get; set; }
    
    Task StartAsync();

    Task StopAsync();
}

public interface IGameHubClient
{
    public Task InitializeMapData(MapData mapData);

    public Task UserDisconnected(string userId);

    public Task InitializeTeamsData(TeamData[] teamData);

    public Task DistrictOwnerChanged(DistrictOwner districtOwner);

    public Task NewLocationReceived(ActorLocation actorLocation);

}
