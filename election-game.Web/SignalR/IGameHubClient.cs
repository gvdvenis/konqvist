using election_game.Data.Contracts;
using election_game.Data.Models;
using Microsoft.AspNetCore.Components;

namespace ElectionGame.Web.SignalR;

public interface IBindableHubClient: IGameHubClient
{
    public EventCallback<string> OnUserDisconnected { get; set; }
    
    public EventCallback<MapData> OnInitializeMapData { get; set; }

    public EventCallback<TeamData[]> OnInitializeTeamsData { get; set; }

    public EventCallback<DistrictOwner> OnDistrictOwnerChanged { get; set; }

    public EventCallback<ActorLocation> OnNewLocationReceived { get; set; }

    public IGameHubServer Server { get; }

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
