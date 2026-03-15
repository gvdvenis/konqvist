using Fluxor;
using Konqvist.Client.Core.Models;

namespace Konqvist.Client.Features.Game.Store;

[FeatureState]
public sealed record GameState
{
    public GamePhase CurrentPhase { get; init; } = GamePhase.WaitingForPlayers;

    public int CurrentRoundNumber { get; init; }

    public int? GameSessionId { get; init; }

    private GameState()
    {
    }
}
