using Konqvist.Client.Core.Models;

namespace Konqvist.Client.Features.Game.Store;

public sealed record GameStartedAction(
    int GameSessionId,
    int CurrentRoundNumber,
    GamePhase CurrentPhase);

public sealed record GamePhaseChangedAction(
    int GameSessionId,
    GamePhase CurrentPhase,
    int? CurrentRoundNumber);

public sealed record GameStateChangedAction(
    int GameSessionId,
    int CurrentRoundNumber,
    GamePhase CurrentPhase);

public sealed record RoundEndedAction(
    int GameSessionId,
    int CurrentRoundNumber,
    GamePhase CurrentPhase);
