using election_game.Data.Contracts;
using election_game.Data.Models;
using Microsoft.AspNetCore.Components;

namespace ElectionGame.Web.SignalR;

public interface IGameHubClient
{
    Task StartAsync();

    Task StopAsync();
    
    public EventCallback<string> UserDisconnected { get; set; }
    
    public EventCallback<MapData> InitializeMapData { get; set; }

    public EventCallback<TeamData[]> InitializeTeamsData { get; set; }

    public EventCallback<DistrictOwner> DistrictOwnerChanged { get; set; }

    public EventCallback<ActorLocation> NewLocationReceived { get; set; }

    public IGameHubServer Server { get; }
}
