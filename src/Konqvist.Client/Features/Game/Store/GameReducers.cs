using Fluxor;
using Konqvist.Client.Core.State;

namespace Konqvist.Client.Features.Game.Store;

public static class GameReducers
{
    [ReducerMethod]
    public static GameState ReduceGameStartedAction(GameState state, GameStartedAction action) =>
        state with
        {
            GameSessionId = action.GameSessionId,
            CurrentRoundNumber = action.CurrentRoundNumber,
            CurrentPhase = action.CurrentPhase
        };

    [ReducerMethod]
    public static GameState ReduceGamePhaseChangedAction(GameState state, GamePhaseChangedAction action) =>
        state with
        {
            GameSessionId = action.GameSessionId,
            CurrentPhase = action.CurrentPhase,
            CurrentRoundNumber = action.CurrentRoundNumber ?? state.CurrentRoundNumber
        };

    [ReducerMethod]
    public static GameState ReduceGameStateChangedAction(GameState state, GameStateChangedAction action) =>
        state with
        {
            GameSessionId = action.GameSessionId,
            CurrentRoundNumber = action.CurrentRoundNumber,
            CurrentPhase = action.CurrentPhase
        };

    [ReducerMethod]
    public static GameState ReduceRoundEndedAction(GameState state, RoundEndedAction action) =>
        state with
        {
            GameSessionId = action.GameSessionId,
            CurrentRoundNumber = action.CurrentRoundNumber,
            CurrentPhase = action.CurrentPhase
        };

    [ReducerMethod]
    public static GameState ReduceFullStateSyncAction(GameState state, FullStateSyncAction action) =>
        state with
        {
            GameSessionId = action.Snapshot.Game.GameSessionId,
            CurrentRoundNumber = action.Snapshot.Game.CurrentRoundNumber,
            CurrentPhase = action.Snapshot.Game.CurrentPhase
        };
}
