using Fluxor;
using Konqvist.Client.Core.Models;

namespace Konqvist.Client.Features.Player.Store;

[FeatureState]
public sealed record PlayerState
{
    public int? PlayerSessionId { get; init; }

    public int? TeamSessionId { get; init; }

    public string? TeamName { get; init; }

    public PlayerRole? Role { get; init; }

    public bool IsLoggedIn { get; init; }

    public bool IsOnline { get; init; }

    private PlayerState()
    {
    }
}
