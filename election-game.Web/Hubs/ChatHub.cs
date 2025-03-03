using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Hubs;

public class BlazorChatSampleHub : Hub
{
    public const string HubUrl = "/chat";

    public async Task Broadcast(string username, string message)
    {
        await Clients.All.SendAsync("Broadcast", username, message);
    }

    public async Task BroadcastNewLocation(string username, Coordinate newLocation)
    {
        await Clients.All.SendAsync("BroadcastNewLocation", username, newLocation);
    }

    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"{Context.ConnectionId} connected");
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        Console.WriteLine($"Disconnected {e?.Message} {Context.ConnectionId}");
        await base.OnDisconnectedAsync(e);
    }
}
